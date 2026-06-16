using Godot;
using System.Collections.Generic;

public partial class PlayerInventory : Node
{
    // These are item resources you can add in the inspector for testing/debug starting inventory.
    [Export]
    public Godot.Collections.Array<Item> StartingItems = new Godot.Collections.Array<Item>();

    // This is the real runtime backpack inventory.
    // This does NOT appear in the inspector.
    public List<ItemInstance> playerInventory = new List<ItemInstance>();

    // This is the fixed-size hotbar.
    public ItemInstance[] playerHotBar = new ItemInstance[5];

    [Signal]
    public delegate void ItemAddedEventHandler(ItemInstance item);

    [Signal]
    public delegate void ItemRemovedEventHandler(ItemInstance item);

    [Signal]
    public delegate void InventoryChangedEventHandler();

    [Signal]
    public delegate void HotbarItemAddedEventHandler(ItemInstance item, int slot);

    [Signal]
    public delegate void HotbarChangedEventHandler();

    public override void _Ready()
    {
        AddStartingItems();
    }

    private void AddStartingItems()
    {
        if (StartingItems == null)
            return;

        for (int i = 0; i < StartingItems.Count; i++)
        {
            Item itemDefinition = StartingItems[i];

            if (itemDefinition == null)
                continue;

            AddItem(itemDefinition);
        }

        GD.Print($"PlayerInventory: Added starting items. Backpack count: {playerInventory.Count}");
    }

    public ItemInstance AddItem(Item itemDefinition)
    {
        if (itemDefinition == null)
            return null;

        // This creates a real runtime instance from the item resource.
        ItemInstance itemInstance = new ItemInstance(itemDefinition);

        AddItemInstance(itemInstance);

        return itemInstance;
    }

    public void AddItemInstance(ItemInstance itemInstance)
    {
        if (itemInstance == null)
            return;

        if (itemInstance.Definition == null)
            return;

        playerInventory.Add(itemInstance);

        GD.Print($"PlayerInventory: Added item to backpack: {itemInstance.Definition.ItemName}");

        EmitSignal(SignalName.ItemAdded, itemInstance);
        EmitSignal(SignalName.InventoryChanged);
    }

    public bool RemoveItemInstance(ItemInstance itemInstance)
    {
        if (itemInstance == null)
            return false;

        bool removed = playerInventory.Remove(itemInstance);

        if (!removed)
            return false;

        GD.Print($"PlayerInventory: Removed item from backpack: {itemInstance.Definition?.ItemName}");

        EmitSignal(SignalName.ItemRemoved, itemInstance);
        EmitSignal(SignalName.InventoryChanged);

        return true;
    }

    public bool RemoveItemEverywhere(ItemInstance itemInstance)
    {
        if (itemInstance == null)
            return false;

        bool removedSomething = false;

        if (playerInventory.Remove(itemInstance))
        {
            EmitSignal(SignalName.ItemRemoved, itemInstance);
            removedSomething = true;
        }

        for (int i = 0; i < playerHotBar.Length; i++)
        {
            if (playerHotBar[i] == itemInstance)
            {
                playerHotBar[i] = null;
                removedSomething = true;
            }
        }

        if (removedSomething)
        {
            GD.Print($"PlayerInventory: Removed item everywhere: {itemInstance.Definition?.ItemName}");

            EmitSignal(SignalName.InventoryChanged);
            EmitSignal(SignalName.HotbarChanged);
        }

        return removedSomething;
    }

    public bool AddItemToHotbar(ItemInstance itemInstance)
    {
        if (itemInstance == null)
            return false;

        if (itemInstance.Definition == null)
            return false;

        for (int i = 0; i < playerHotBar.Length; i++)
        {
            if (playerHotBar[i] == null)
            {
                playerHotBar[i] = itemInstance;

                GD.Print($"PlayerInventory: Added item to hotbar slot {i}: {itemInstance.Definition.ItemName}");

                EmitSignal(SignalName.HotbarItemAdded, itemInstance, i);
                EmitSignal(SignalName.HotbarChanged);

                return true;
            }
        }

        GD.Print("PlayerInventory: Hotbar full.");
        return false;
    }

    public bool AddItemToHotbarSlot(ItemInstance itemInstance, int slot)
    {
        if (itemInstance == null)
            return false;

        if (itemInstance.Definition == null)
            return false;

        if (!IsValidHotbarSlot(slot))
            return false;

        if (playerHotBar[slot] != null)
            return false;

        playerHotBar[slot] = itemInstance;

        GD.Print($"PlayerInventory: Added item to hotbar slot {slot}: {itemInstance.Definition.ItemName}");

        EmitSignal(SignalName.HotbarItemAdded, itemInstance, slot);
        EmitSignal(SignalName.HotbarChanged);

        return true;
    }

    public ItemInstance GetHotbarItem(int slot)
    {
        if (!IsValidHotbarSlot(slot))
            return null;

        return playerHotBar[slot];
    }

    public bool RemoveHotbarItemAtSlot(int slot)
    {
        if (!IsValidHotbarSlot(slot))
            return false;

        ItemInstance itemInstance = playerHotBar[slot];

        if (itemInstance == null)
            return false;

        playerHotBar[slot] = null;

        GD.Print($"PlayerInventory: Removed hotbar item from slot {slot}: {itemInstance.Definition?.ItemName}");

        EmitSignal(SignalName.HotbarChanged);

        return true;
    }

    public bool MoveHotbarItemToBackpack(int slot)
    {
        if (!IsValidHotbarSlot(slot))
            return false;

        ItemInstance itemInstance = playerHotBar[slot];

        if (itemInstance == null)
            return false;

        if (itemInstance.Definition == null)
            return false;

        playerHotBar[slot] = null;
        playerInventory.Add(itemInstance);

        GD.Print($"PlayerInventory: Moved hotbar slot {slot} to backpack: {itemInstance.Definition.ItemName}");

        EmitSignal(SignalName.ItemAdded, itemInstance);
        EmitSignal(SignalName.InventoryChanged);
        EmitSignal(SignalName.HotbarChanged);

        return true;
    }

    public bool MoveBackpackItemToHotbarSlot(ItemInstance itemInstance, int slot)
    {
        if (itemInstance == null)
            return false;

        if (itemInstance.Definition == null)
            return false;

        if (!IsValidHotbarSlot(slot))
            return false;

        if (playerHotBar[slot] != null)
            return false;

        bool removedFromBackpack = playerInventory.Remove(itemInstance);

        if (!removedFromBackpack)
            return false;

        playerHotBar[slot] = itemInstance;

        GD.Print($"PlayerInventory: Moved backpack item to hotbar slot {slot}: {itemInstance.Definition.ItemName}");

        EmitSignal(SignalName.ItemRemoved, itemInstance);
        EmitSignal(SignalName.InventoryChanged);
        EmitSignal(SignalName.HotbarItemAdded, itemInstance, slot);
        EmitSignal(SignalName.HotbarChanged);

        return true;
    }

    public bool SwapHotbarSlots(int firstSlot, int secondSlot)
    {
        if (!IsValidHotbarSlot(firstSlot))
            return false;

        if (!IsValidHotbarSlot(secondSlot))
            return false;

        if (firstSlot == secondSlot)
            return true;

        ItemInstance firstItem = playerHotBar[firstSlot];
        ItemInstance secondItem = playerHotBar[secondSlot];

        playerHotBar[firstSlot] = secondItem;
        playerHotBar[secondSlot] = firstItem;

        GD.Print($"PlayerInventory: Swapped hotbar slots {firstSlot} and {secondSlot}.");

        EmitSignal(SignalName.HotbarChanged);

        return true;
    }

    public bool SwapBackpackItemWithHotbarSlot(ItemInstance backpackItem, int hotbarSlot)
    {
        if (backpackItem == null)
            return false;

        if (backpackItem.Definition == null)
            return false;

        if (!IsValidHotbarSlot(hotbarSlot))
            return false;

        int backpackIndex = playerInventory.IndexOf(backpackItem);

        if (backpackIndex < 0)
            return false;

        ItemInstance hotbarItem = playerHotBar[hotbarSlot];

        playerInventory[backpackIndex] = hotbarItem;
        playerHotBar[hotbarSlot] = backpackItem;

        // If the hotbar slot was empty, remove the null that would otherwise be put into the backpack.
        if (hotbarItem == null)
        {
            playerInventory.RemoveAt(backpackIndex);
        }

        GD.Print($"PlayerInventory: Swapped backpack item with hotbar slot {hotbarSlot}: {backpackItem.Definition.ItemName}");

        EmitSignal(SignalName.InventoryChanged);
        EmitSignal(SignalName.HotbarChanged);

        return true;
    }

    public bool IsItemInBackpack(ItemInstance itemInstance)
    {
        if (itemInstance == null)
            return false;

        return playerInventory.Contains(itemInstance);
    }

    public bool IsItemInHotbar(ItemInstance itemInstance)
    {
        if (itemInstance == null)
            return false;

        for (int i = 0; i < playerHotBar.Length; i++)
        {
            if (playerHotBar[i] == itemInstance)
                return true;
        }

        return false;
    }

    public int GetHotbarSlotOfItem(ItemInstance itemInstance)
    {
        if (itemInstance == null)
            return -1;

        for (int i = 0; i < playerHotBar.Length; i++)
        {
            if (playerHotBar[i] == itemInstance)
                return i;
        }

        return -1;
    }

    private bool IsValidHotbarSlot(int slot)
    {
        return slot >= 0 && slot < playerHotBar.Length;
    }

    public IReadOnlyList<ItemInstance> GetBackpackItems()
    {
        return playerInventory;
    }

    public int GetBackpackCount()
    {
        return playerInventory.Count;
    }
}