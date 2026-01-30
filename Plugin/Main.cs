using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using Fusion;
using HarmonyLib;
using System.ComponentModel;
using UnityEngine;

[BepInPlugin("com.ic23.autoloot", "AutoLoot", "1.0.1")]
public class AutoLootPlugin : BaseUnityPlugin
{
	internal static class Logg
	{
		internal const string Prefix = "[AutoLoot] ";
		internal static readonly ManualLogSource Logger = BepInEx.Logging.Logger.CreateLogSource("AutoLoot");
		internal static void Debug(object data) => Logger.Log(BepInEx.Logging.LogLevel.Debug, Prefix + (data?.ToString() ?? "null"));
		internal static void Info(object data) => Logger.Log(BepInEx.Logging.LogLevel.Info, Prefix + (data?.ToString() ?? "null"));
		internal static void Message(object data) => Logger.Log(BepInEx.Logging.LogLevel.Info, Prefix + (data?.ToString() ?? "null"));
		internal static void Warning(object data) => Logger.Log(BepInEx.Logging.LogLevel.Warning, Prefix + (data?.ToString() ?? "null"));
		internal static void Error(object data) => Logger.Log(BepInEx.Logging.LogLevel.Error, Prefix + (data?.ToString() ?? "null"));
		internal static void Fatal(object data) => Logger.Log(BepInEx.Logging.LogLevel.Fatal, Prefix + (data?.ToString() ?? "null"));
	}

	// Configuration settings
	public static ConfigEntry<float> CollectRadiusConfig;

	// Public property to access the radius
	public static float CollectRadius => CollectRadiusConfig.Value;

	private void Awake()
	{
		// Initialize configuration
		CollectRadiusConfig = Config.Bind(
			"General",                    // Section
			"CollectRadius",              // Key
			2000f,                        // Default value
			"Item collection radius in meters" // Description
		);

		var harmony = new Harmony("com.ic23.autoloot");
		harmony.PatchAll();
		Logg.Info($"Plugin loading is completed. CollectRadius: {CollectRadius}");
	}
}

// Patch for skillbooks in inventory slots
[HarmonyPatch(typeof(PlayerInventory), "SendLootToTruck")]
public static class PlayerInventory_SendLootToTruck_SkillBook_Patch
{
	static void Prefix(PlayerInventory __instance)
	{
		var player = __instance.player;
		if (player == null || player.Runner == null) return;

		var truck = GameObject.FindGameObjectWithTag("SpawnLootTruck");
		if (truck == null) return;

		for (int i = 0; i < __instance.itemSlots.Count; i++)
		{
			var slot = __instance.itemSlots[i];
			if (slot.assignedItem == null) continue;

			// Only skillbooks that are NOT isLoot (isLoot will be handled by the original method)
			if (!slot.assignedItem.isSkillBook || slot.assignedItem.isLoot) continue;

			while (slot.assignedItem != null && slot.amount > 0)
			{
				var netObj = player.Runner.Spawn(
					slot.assignedItem.itemDropPrefab,
					new Vector3?(truck.transform.position),
					new Quaternion?(Quaternion.identity),
					null, null, (NetworkSpawnFlags)0
				);

				if (netObj != null)
				{
					netObj.transform.eulerAngles += slot.assignedItem.placementExtraRotation;
					__instance.SetDroppedValues(
						netObj.GetComponent<Pickupable>(),
						slot.ReturnCurrentDamage(),
						slot.ReturnCurrentProperty()
					);
				}

				slot.RemoveFromSlot();
			}
		}
	}
}


// Patch for heavy items and skillbooks
[HarmonyPatch(typeof(PlayerInventory), "SendLootToTruck")]
public static class PlayerInventory_SendLootToTruck_Patch
{
	static void Postfix(PlayerInventory __instance)
	{
		var player = __instance.player;
		if (player == null || player.Runner == null) return;

		WeaponManager weaponManager = null;
		try
		{
			weaponManager = __instance.weaponManager;
		}
		catch
		{
			var wmField = AccessTools.Field(typeof(PlayerInventory), "weaponManager");
			weaponManager = wmField?.GetValue(__instance) as WeaponManager;
		}
		if (weaponManager == null) return;
		var carriedItem = weaponManager.carriedItem;
		if (carriedItem == null || !carriedItem.isLoot) return;

		// Find Truck
		var truck = GameObject.FindGameObjectWithTag("SpawnLootTruck");
		if (truck == null) return;

		var netObj = player.Runner.Spawn(
				carriedItem.itemDropPrefab,
				new Vector3?(truck.transform.position),
				new Quaternion?(Quaternion.identity),
				null, null, (NetworkSpawnFlags)0
		);

		if (netObj != null)
		{
			netObj.transform.eulerAngles += carriedItem.placementExtraRotation;

			var pickup = netObj.GetComponent<Pickupable>();
			if (pickup != null)
			{
				__instance.SetDroppedValues(
					pickup,
					weaponManager.carriedItemDamage,
					weaponManager.carriedItemCustomProperty
				);
				pickup.SpawnMarker();
			}
		}
		weaponManager.carriedItemDamage = 0f;
		weaponManager.carriedItemCustomProperty = "";
		weaponManager.carriedItem = null;
		weaponManager.isCarrying = false;
		weaponManager.currentLootItem = "";

		__instance.ActivateCurrentSlot();
	}

}

// Collect all nearby items and teleport them to the truck
public static class AutoLootCollector
{
	private static bool isCollecting = false;
	private static FPP_Player lastPlayer = null;

	public static void CollectAllNearbyItems(FPP_Player player)
	{
		// lastPlayer == null will trigger if:
		// 1. First run (lastPlayer was never set)
		// 2. Object was destroyed (Destroy) — Unity will return true
		// 
		// lastPlayer != player will trigger if:
		// 1. A new player object was created (different "address" in memory)
		if (lastPlayer == null || lastPlayer != player)
		{
			if (isCollecting)
			{
				AutoLootPlugin.Logg.Info("Previous collection was interrupted, resetting flag");
			}
			isCollecting = false;
		}
		lastPlayer = player;

		if (isCollecting)
		{
			AutoLootPlugin.Logg.Warning("Collection already in progress");
			return;
		}

		if (player == null || player.Runner == null)
		{
			AutoLootPlugin.Logg.Warning("Player or Runner is null");
			return;
		}

		// Find the truck
		var truck = GameObject.FindGameObjectWithTag("SpawnLootTruck");
		if (truck == null)
		{
			AutoLootPlugin.Logg.Warning("Truck not found!");
			return;
		}

		player.StartCoroutine(CollectItemsCoroutine(player));
	}

	private static System.Collections.IEnumerator CollectItemsCoroutine(FPP_Player player)
	{
		isCollecting = true;
		try
		{
			Vector3 playerPosition = player.transform.position;
			var playerNetworkObject = player.GetComponent<NetworkObject>();

			if (playerNetworkObject == null)
			{
				AutoLootPlugin.Logg.Error("NetworkObject not found on player");
				yield break;
			}

			// Get the required quota
			float requiredValue = 0f;
			try
			{
				requiredValue = player.WM.quotaManager.requiredQuota - player.WM.quotaManager.currentQuota;
				AutoLootPlugin.Logg.Info($"Required quota: {requiredValue}");
			}
			catch (System.Exception ex)
			{
				AutoLootPlugin.Logg.Warning($"Could not get requiredQuota: {ex.Message}");
			}

			// Collect the list of items in advance
			var allPickupables = GameObject.FindObjectsOfType<Pickupable>();
			var itemsToCollect = new System.Collections.Generic.List<Pickupable>();

			foreach (var pickupable in allPickupables)
			{
				if (pickupable == null) continue;

				float distance = Vector3.Distance(playerPosition, pickupable.transform.position);
				if (distance > AutoLootPlugin.CollectRadius) continue;

				if (pickupable.assignedItem == null) continue;
				if (!pickupable.assignedItem.isLoot && !pickupable.assignedItem.isSkillBook) continue;
				if (pickupable.blockPickup) continue;
				if (pickupable.assignedItem.requireSkillToCarry)
				{
					if (!player.skillManager.CheckIfSkillLearned(pickupable.assignedItem.skillIDToCarry)) continue;
				}

				itemsToCollect.Add(pickupable);
			}

			AutoLootPlugin.Logg.Info($"Found {itemsToCollect.Count} items to collect");

			// Sort by value from highest to lowest
			itemsToCollect.Sort((a, b) => b.assignedItem.value.CompareTo(a.assignedItem.value));

			// Move all skillbooks to the beginning of the list (preserving their order among themselves)
			var skillBooks = itemsToCollect.FindAll(p => p.assignedItem.isSkillBook);
			var otherItems = itemsToCollect.FindAll(p => !p.assignedItem.isSkillBook);
			itemsToCollect.Clear();
			itemsToCollect.AddRange(skillBooks);
			itemsToCollect.AddRange(otherItems);

			int collectedCount = 0;

			foreach (var pickupable in itemsToCollect)
			{
				// Quota check - if enough has been collected or quota already met, finish (except for skillbooks)
				if (!pickupable.assignedItem.isSkillBook)
				{
					// Skip regular loot if quota is already fulfilled (requiredValue <= 0)
					if (requiredValue <= 0)
					{
						continue;
					}

					float totalValue = Pinboard.Instance.collectedLootValue;
					if (totalValue >= requiredValue)
					{
						AutoLootPlugin.Logg.Info($"Quota reached! Collected: {totalValue} / Required: {requiredValue}");
						break;
					}
				}

				// Check that the item still exists and is available
				if (pickupable == null) continue;
				if (pickupable.blockPickup) continue;

				var pickupableNetObj = pickupable.GetComponent<NetworkObject>();
				if (pickupableNetObj == null) continue;

				bool success = false;

				try
				{
					// 1. First, clear the inventory
					player.inventory.SendLootToTruck();
					success = true;
				}
				catch (System.Exception ex)
				{
					AutoLootPlugin.Logg.Error($"Error SendLootToTruck: {ex.Message}");
				}

				if (!success) continue;

				// 2. Small delay to allow the inventory to clear
				yield return new WaitForSeconds(0.05f);

				try
				{
					// 3. Pick up the item
					if (player.CheckIfItemNearbyOtherPlayers(pickupable))
					{
						pickupable.RPC_RequestPickup(playerNetworkObject.Id);
					}
					else
					{
						player.RPC_PickUp(pickupableNetObj.Id);
					}

					collectedCount++;
					AutoLootPlugin.Logg.Debug($"Picked up: {pickupable.assignedItem.itemName}");
				}
				catch (System.Exception ex)
				{
					AutoLootPlugin.Logg.Error($"Error RPC_PickUp: {ex.Message}");
				}

				// 4. Wait for the item to enter the inventory
				yield return new WaitForSeconds(0.15f);
			}

			// Final send of the last item
			yield return new WaitForSeconds(0.1f);

			try
			{
				player.inventory.SendLootToTruck();
			}
			catch (System.Exception ex)
			{
				AutoLootPlugin.Logg.Error($"Error final SendLootToTruck: {ex.Message}");
			}

			float finalValue = Pinboard.Instance.collectedLootValue;
			AutoLootPlugin.Logg.Info($"Collected {collectedCount} items. Total value: {finalValue} / Required: {requiredValue}");
		}
		finally
		{
			isCollecting = false;
		}
	}
}

// Hijack to add keybind
[HarmonyPatch(typeof(ThermalVision), "Update")]
class ThermalVision_Update_Patch
{
	static void Postfix(ThermalVision __instance)
	{
		if (__instance.player == null) return;

		// N - collect ALL nearby items and teleport them to the truck
		if (Input.GetKeyDown(KeyCode.N))
		{
			AutoLootCollector.CollectAllNearbyItems(__instance.player);
		}
	}
}