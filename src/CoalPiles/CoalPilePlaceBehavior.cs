using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

namespace CoalPiles
{
    public class CoalPilePlaceBehavior : CollectibleBehavior
    {
        private const string NextAddAttr = "coalpiles_nextAdd";
        private const float InitialHoldDelay = 0.25f;
        private const float SingleRepeatInterval = 0.08f; // ~12 items/sec for Ctrl + Right-Click
        private const float BulkRepeatInterval = 0.15f;   // 16 items per tick for Ctrl + Shift + Right-Click

        public CoalPilePlaceBehavior(CollectibleObject collObj) : base(collObj) { }

        public override void OnHeldInteractStart(
            ItemSlot slot,
            EntityAgent byEntity,
            BlockSelection blockSel,
            EntitySelection entitySel,
            bool firstEvent,
            ref EnumHandHandling handHandling,
            ref EnumHandling handling)
        {
            if (!firstEvent || blockSel == null || slot?.Itemstack == null) return;

            IWorldAccessor world = byEntity.World;
            Block clickedBlock = world.BlockAccessor.GetBlock(blockSel.Position);

            // Case 1: Aiming at an existing pile -> Ctrl adds 1, Ctrl + Shift adds up to 16 (and stacks infinitely upward when full)
            if (clickedBlock is BlockCoalPile)
            {
                if (!byEntity.Controls.CtrlKey) return;

                handHandling = EnumHandHandling.PreventDefaultAction;
                handling = EnumHandling.PreventDefault;

                if (TryAddOrStackUp(world, slot, byEntity, blockSel.Position))
                {
                    byEntity.Attributes.SetFloat(NextAddAttr, InitialHoldDelay);
                }

                return;
            }

            // Case 2: Aiming at regular ground -> Ctrl places 1 (or 16 with Shift) and creates a new pile
            if (byEntity.Controls.CtrlKey)
            {
                Block pileBlock = world.GetBlock(new AssetLocation(CoalPilesModSystem.PileBlockCode));
                if (pileBlock == null) return;

                BlockPos targetPos = blockSel.DidOffset ? blockSel.Position : blockSel.Position.AddCopy(blockSel.Face);
                Block targetBlock = world.BlockAccessor.GetBlock(targetPos);
                Block belowBlock = world.BlockAccessor.GetBlock(targetPos.DownCopy());

                if (!belowBlock.CanAttachBlockAt(world.BlockAccessor, pileBlock, targetPos.DownCopy(), BlockFacing.UP))
                {
                    return;
                }

                if (targetBlock.IsReplacableBy(pileBlock))
                {
                    bool isCreative = IsCreativePlayer(byEntity);
                    int wanted = byEntity.Controls.ShiftKey ? BlockEntityCoalPile.ItemsPerLayer : 1;
                    int available = isCreative ? wanted : slot.StackSize;
                    int toAdd = GameMath.Min(wanted, GameMath.Min(BlockEntityCoalPile.MaxCount, available));
                    if (toAdd <= 0) return;

                    string itemPath = slot.Itemstack.Collectible.Code.Path;
                    string itemName = slot.Itemstack.GetName();

                    if (world.Side == EnumAppSide.Server)
                    {
                        world.BlockAccessor.SetBlock(pileBlock.BlockId, targetPos);
                        var be = world.BlockAccessor.GetBlockEntity(targetPos) as BlockEntityCoalPile;
                        if (be != null)
                        {
                            be.ItemCode = itemPath;
                            be.Count = toAdd;
                            be.MarkDirty(true);
                        }

                        if (!isCreative)
                        {
                            slot.TakeOut(toAdd);
                            slot.MarkDirty();
                        }
                    }

                    CoalPilesModSystem.LogChat(world, $"Placed {toAdd} {itemName} ({toAdd}/{BlockEntityCoalPile.MaxCount})");

                    handHandling = EnumHandHandling.PreventDefaultAction;
                    handling = EnumHandling.PreventDefault;
                }
            }
        }

        public override bool OnHeldInteractStep(
            float secondsUsed,
            ItemSlot slot,
            EntityAgent byEntity,
            BlockSelection blockSel,
            EntitySelection entitySel,
            ref EnumHandling handling)
        {
            if (blockSel == null || slot?.Itemstack == null || !byEntity.Controls.CtrlKey)
            {
                return false;
            }

            IWorldAccessor world = byEntity.World;
            if (!(world.BlockAccessor.GetBlock(blockSel.Position) is BlockCoalPile))
            {
                return false;
            }

            handling = EnumHandling.PreventDefault;

            float nextAdd = byEntity.Attributes.GetFloat(NextAddAttr, InitialHoldDelay);
            if (secondsUsed < nextAdd)
            {
                return true;
            }

            float interval = byEntity.Controls.ShiftKey ? BulkRepeatInterval : SingleRepeatInterval;
            byEntity.Attributes.SetFloat(NextAddAttr, secondsUsed + interval);
            return TryAddOrStackUp(world, slot, byEntity, blockSel.Position);
        }

        private static bool IsCreativePlayer(EntityAgent byEntity)
        {
            return (byEntity as EntityPlayer)?.Player?.WorldData?.CurrentGameMode == EnumGameMode.Creative;
        }

        private bool TryAddOrStackUp(IWorldAccessor world, ItemSlot slot, EntityAgent byEntity, BlockPos pos)
        {
            var clickedBe = world.BlockAccessor.GetBlockEntity(pos) as BlockEntityCoalPile;
            string itemPath = slot.Itemstack.Collectible.Code.Path;
            if (clickedBe == null || clickedBe.ItemCode != itemPath) return false;

            bool isCreative = IsCreativePlayer(byEntity);
            int wanted = byEntity.Controls.ShiftKey ? BlockEntityCoalPile.ItemsPerLayer : 1;
            int available = isCreative ? wanted : slot.StackSize;
            if (available <= 0) return false;

            string itemName = slot.Itemstack.GetName();

            // 1. Fill clicked pile first if it still has room
            if (clickedBe.Count < BlockEntityCoalPile.MaxCount)
            {
                int spaceLeft = BlockEntityCoalPile.MaxCount - clickedBe.Count;
                int toAdd = GameMath.Min(wanted, GameMath.Min(spaceLeft, available));
                if (toAdd <= 0) return false;

                int newCount = clickedBe.Count + toAdd;

                if (world.Side == EnumAppSide.Server)
                {
                    if (!isCreative)
                    {
                        slot.TakeOut(toAdd);
                        slot.MarkDirty();
                    }

                    clickedBe.Count = newCount;
                    clickedBe.MarkDirty(true);

                    if (newCount >= BlockEntityCoalPile.MaxCount)
                    {
                        world.BlockAccessor.TriggerNeighbourBlockUpdate(pos);
                    }
                }

                CoalPilesModSystem.LogChat(world, $"Added {toAdd} {itemName} ({newCount}/{BlockEntityCoalPile.MaxCount})");
                return true;
            }

            // 2. Clicked pile is full -> walk upward past full piles of the same coal to stack infinitely up
            BlockPos scanPos = pos.UpCopy();
            int mapHeight = world.BlockAccessor.MapSizeY;

            while (scanPos.Y < mapHeight)
            {
                Block scanBlock = world.BlockAccessor.GetBlock(scanPos);
                if (scanBlock is BlockCoalPile)
                {
                    var upBe = world.BlockAccessor.GetBlockEntity(scanPos) as BlockEntityCoalPile;
                    if (upBe == null || upBe.ItemCode != itemPath)
                    {
                        return false; // Blocked by a different fuel pile above
                    }

                    if (upBe.Count < BlockEntityCoalPile.MaxCount)
                    {
                        int spaceLeft = BlockEntityCoalPile.MaxCount - upBe.Count;
                        int toAdd = GameMath.Min(wanted, GameMath.Min(spaceLeft, available));
                        if (toAdd <= 0) return false;

                        int newCount = upBe.Count + toAdd;

                        if (world.Side == EnumAppSide.Server)
                        {
                            if (!isCreative)
                            {
                                slot.TakeOut(toAdd);
                                slot.MarkDirty();
                            }

                            upBe.Count = newCount;
                            upBe.MarkDirty(true);

                            if (newCount >= BlockEntityCoalPile.MaxCount)
                            {
                                world.BlockAccessor.TriggerNeighbourBlockUpdate(scanPos);
                            }
                        }

                        CoalPilesModSystem.LogChat(world, $"Stacked {toAdd} {itemName} above ({newCount}/{BlockEntityCoalPile.MaxCount})");
                        return true;
                    }

                    scanPos.Up();
                    continue;
                }

                // Found an open block above the top full pile -> create a new pile block there
                Block pileBlock = world.GetBlock(new AssetLocation(CoalPilesModSystem.PileBlockCode));
                if (pileBlock == null || !scanBlock.IsReplacableBy(pileBlock))
                {
                    return false;
                }

                int createCount = GameMath.Min(wanted, GameMath.Min(BlockEntityCoalPile.MaxCount, available));
                if (createCount <= 0) return false;

                if (world.Side == EnumAppSide.Server)
                {
                    world.BlockAccessor.SetBlock(pileBlock.BlockId, scanPos);
                    var newBe = world.BlockAccessor.GetBlockEntity(scanPos) as BlockEntityCoalPile;
                    if (newBe != null)
                    {
                        newBe.ItemCode = itemPath;
                        newBe.Count = createCount;
                        newBe.MarkDirty(true);
                    }

                    if (!isCreative)
                    {
                        slot.TakeOut(createCount);
                        slot.MarkDirty();
                    }

                    world.PlaySoundAt(new AssetLocation("game:block/gravel"), scanPos.X + 0.5, scanPos.Y + 0.5, scanPos.Z + 0.5, null, true, 16f);
                }

                CoalPilesModSystem.LogChat(world, $"Started upper pile with {createCount} {itemName} ({createCount}/{BlockEntityCoalPile.MaxCount})");
                return true;
            }

            return false;
        }
    }
}