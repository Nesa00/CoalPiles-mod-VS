using System;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
// using Vintagestory.GameContent; // [FIRE DISABLED] Only needed for IIgnitable

namespace CoalPiles
{
    public class BlockCoalPile : Block // , IIgnitable // [FIRE DISABLED]
    {
        // [FIRE DISABLED]
        // private static readonly byte[] BurningLightHsv = { 3, 7, 10 };

        // 10 layers x 0.1f each = 1.0 full building block at 160 items
        private static readonly Cuboidf[][] LayerBoxes =
        {
            new[] { new Cuboidf(0f, 0f, 0f, 1f, 0.1f, 1f) }, // Layer 1 (1-16)
            new[] { new Cuboidf(0f, 0f, 0f, 1f, 0.2f, 1f) }, // Layer 2 (17-32)
            new[] { new Cuboidf(0f, 0f, 0f, 1f, 0.3f, 1f) }, // Layer 3 (33-48)
            new[] { new Cuboidf(0f, 0f, 0f, 1f, 0.4f, 1f) }, // Layer 4 (49-64)
            new[] { new Cuboidf(0f, 0f, 0f, 1f, 0.5f, 1f) }, // Layer 5 (65-80)
            new[] { new Cuboidf(0f, 0f, 0f, 1f, 0.6f, 1f) }, // Layer 6 (81-96)
            new[] { new Cuboidf(0f, 0f, 0f, 1f, 0.7f, 1f) }, // Layer 7 (97-112)
            new[] { new Cuboidf(0f, 0f, 0f, 1f, 0.8f, 1f) }, // Layer 8 (113-128)
            new[] { new Cuboidf(0f, 0f, 0f, 1f, 0.9f, 1f) }, // Layer 9 (129-144)
            new[] { new Cuboidf(0f, 0f, 0f, 1f, 1.0f, 1f) }, // Layer 10 (145-160)
        };

        private Cuboidf[] GetPileBoxes(IBlockAccessor blockAccessor, BlockPos pos)
        {
            var be = blockAccessor.GetBlockEntity(pos) as BlockEntityCoalPile;
            int count = be?.Count ?? 1;
            int layerIdx = GameMath.Clamp((count - 1) / BlockEntityCoalPile.ItemsPerLayer, 0, LayerBoxes.Length - 1);
            return LayerBoxes[layerIdx];
        }

        public override Cuboidf[] GetCollisionBoxes(IBlockAccessor blockAccessor, BlockPos pos) => GetPileBoxes(blockAccessor, pos);
        public override Cuboidf[] GetSelectionBoxes(IBlockAccessor blockAccessor, BlockPos pos) => GetPileBoxes(blockAccessor, pos);

        public override bool CanAttachBlockAt(IBlockAccessor world, Block block, BlockPos pos, BlockFacing blockFace, Cuboidi attachmentArea = null)
        {
            if (blockFace == BlockFacing.UP)
            {
                var be = world.GetBlockEntity(pos) as BlockEntityCoalPile;
                if (be != null && be.Count >= BlockEntityCoalPile.MaxCount)
                {
                    return true;
                }
            }

            return base.CanAttachBlockAt(world, block, pos, blockFace, attachmentArea);
        }

        /* [FIRE DISABLED]
        public override byte[] GetLightHsv(IBlockAccessor blockAccessor, BlockPos pos, ItemStack stack = null)
        {
            if (pos != null)
            {
                var be = blockAccessor.GetBlockEntity(pos) as BlockEntityCoalPile;
                if (be != null && be.IsBurning)
                {
                    return BurningLightHsv;
                }
            }

            return base.GetLightHsv(blockAccessor, pos, stack);
        }

        public EnumIgniteState OnTryIgniteStack(EntityAgent byEntity, BlockPos pos, ItemSlot slot, float secondsIgniting)
        {
            return EnumIgniteState.NotIgnitable;
        }

        public EnumIgniteState OnTryIgniteBlock(EntityAgent byEntity, BlockPos pos, float secondsIgniting)
        {
            var be = byEntity.World.BlockAccessor.GetBlockEntity(pos) as BlockEntityCoalPile;
            if (be == null || be.IsBurning) return EnumIgniteState.NotIgnitablePreventDefault;

            return secondsIgniting > 1.5f ? EnumIgniteState.IgniteNow : EnumIgniteState.Ignitable;
        }

        public void OnTryIgniteBlockOver(EntityAgent byEntity, BlockPos pos, float secondsIgniting, ref EnumHandling handling)
        {
            if (secondsIgniting < 1.45f) return;

            handling = EnumHandling.PreventDefault;
            if (byEntity.World.Side != EnumAppSide.Server) return;

            var be = byEntity.World.BlockAccessor.GetBlockEntity(pos) as BlockEntityCoalPile;
            be?.TryIgnite();
        }

        public override void OnEntityCollide(IWorldAccessor world, Entity entity, BlockPos pos, BlockFacing facing, Vec3d collideSpeed, bool isImpact)
        {
            base.OnEntityCollide(world, entity, pos, facing, collideSpeed, isImpact);

            if (world.Side == EnumAppSide.Server && facing == BlockFacing.UP)
            {
                var be = world.BlockAccessor.GetBlockEntity(pos) as BlockEntityCoalPile;
                if (be != null && be.IsBurning && world.Rand.NextDouble() < 0.25)
                {
                    entity.ReceiveDamage(new DamageSource
                    {
                        Source = EnumDamageSource.Block,
                        SourceBlock = this,
                        Type = EnumDamageType.Fire,
                        SourcePos = pos.ToVec3d()
                    }, 0.5f);
                }
            }
        }
        */

        public override void OnNeighbourBlockChange(IWorldAccessor world, BlockPos pos, BlockPos neibpos)
        {
            base.OnNeighbourBlockChange(world, pos, neibpos);

            if (world.Side != EnumAppSide.Server) return;

            BlockPos belowPos = pos.DownCopy();
            Block belowBlock = world.BlockAccessor.GetBlock(belowPos);
            if (belowBlock.CanAttachBlockAt(world.BlockAccessor, this, belowPos, BlockFacing.UP))
            {
                return;
            }

            // Drop straight down to the lowest replaceable block
            BlockPos targetPos = pos.Copy();
            while (targetPos.Y > 0)
            {
                BlockPos nextDown = targetPos.DownCopy();
                Block nextBlock = world.BlockAccessor.GetBlock(nextDown);
                if (nextBlock.IsReplacableBy(this))
                {
                    targetPos = nextDown;
                }
                else
                {
                    break;
                }
            }

            if (targetPos.Equals(pos)) return;

            var oldBe = world.BlockAccessor.GetBlockEntity(pos) as BlockEntityCoalPile;
            string itemCode = oldBe?.ItemCode ?? CoalPilesModSystem.DefaultFuelCode;
            int count = oldBe?.Count ?? 1;
            // bool wasBurning = oldBe?.IsBurning ?? false; // [FIRE DISABLED]

            BlockPos supportPos = targetPos.DownCopy();
            Block supportBlock = world.BlockAccessor.GetBlock(supportPos);

            world.BlockAccessor.SetBlock(0, pos);

            if (supportBlock.CanAttachBlockAt(world.BlockAccessor, this, supportPos, BlockFacing.UP))
            {
                world.BlockAccessor.SetBlock(BlockId, targetPos);
                var newBe = world.BlockAccessor.GetBlockEntity(targetPos) as BlockEntityCoalPile;
                if (newBe != null)
                {
                    newBe.ItemCode = itemCode;
                    newBe.Count = count;
                    /* [FIRE DISABLED]
                    if (wasBurning)
                    {
                        newBe.TryIgnite();
                    }
                    else
                    {
                        newBe.MarkDirty(true);
                    }
                    */
                    newBe.MarkDirty(true);
                }

                world.PlaySoundAt(new AssetLocation("game:block/gravel"), targetPos.X + 0.5, targetPos.Y + 0.5, targetPos.Z + 0.5, null, true, 16f);
            }
            else
            {
                Item fuelItem = CoalPilesModSystem.GetFuelItem(world, itemCode);
                if (fuelItem != null && count > 0)
                {
                    world.SpawnItemEntity(new ItemStack(fuelItem, count), targetPos.ToVec3d().Add(0.5, 0.25, 0.5));
                }
            }
        }

        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            if (byPlayer == null || blockSel == null) return false;

            var be = world.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityCoalPile;
            if (be == null) return false;

            // [FIRE DISABLED]
            // Don't allow picking up burning coal with bare hands
            // if (be.IsBurning) return false;

            // Plain Right-Click (1) or Shift + Right-Click (16) without Ctrl -> TAKE fuel
            if (!byPlayer.Entity.Controls.CtrlKey && be.Count > 0)
            {
                int wanted = byPlayer.Entity.Controls.ShiftKey ? BlockEntityCoalPile.ItemsPerLayer : 1;
                int toTake = GameMath.Min(wanted, be.Count);
                int remaining = be.Count - toTake;

                Item fuelItem = CoalPilesModSystem.GetFuelItem(world, be.ItemCode);
                string itemName = CoalPilesModSystem.GetFuelName(world, be.ItemCode);

                if (world.Side == EnumAppSide.Server)
                {
                    if (fuelItem != null)
                    {
                        ItemStack taken = new ItemStack(fuelItem, toTake);
                        if (!byPlayer.InventoryManager.TryGiveItemstack(taken))
                        {
                            world.SpawnItemEntity(taken, blockSel.Position.ToVec3d().Add(0.5, 0.25, 0.5));
                        }
                    }

                    be.Count = remaining;
                    if (be.Count <= 0)
                    {
                        world.BlockAccessor.SetBlock(0, blockSel.Position);
                    }
                    else
                    {
                        be.MarkDirty(true);
                        world.BlockAccessor.TriggerNeighbourBlockUpdate(blockSel.Position);
                    }
                }

                CoalPilesModSystem.LogChat(world, $"Took {toTake} {itemName} (Remaining={remaining}/{BlockEntityCoalPile.MaxCount})");
                return true;
            }

            return false;
        }

        public override ItemStack[] GetDrops(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1f)
        {
            var be = world.BlockAccessor.GetBlockEntity(pos) as BlockEntityCoalPile;
            if (be != null && be.Count > 0)
            {
                Item fuelItem = CoalPilesModSystem.GetFuelItem(world, be.ItemCode);
                if (fuelItem != null)
                {
                    return new[] { new ItemStack(fuelItem, be.Count) };
                }
            }

            return Array.Empty<ItemStack>();
        }

        public override string GetPlacedBlockInfo(IWorldAccessor world, BlockPos pos, IPlayer forPlayer)
        {
            var be = world.BlockAccessor.GetBlockEntity(pos) as BlockEntityCoalPile;
            if (be != null)
            {
                string itemName = CoalPilesModSystem.GetFuelName(world, be.ItemCode);
                // string status = be.IsBurning ? " (Burning)" : ""; // [FIRE DISABLED]
                return $"{itemName}: {be.Count} / {BlockEntityCoalPile.MaxCount}";
            }
            return base.GetPlacedBlockInfo(world, pos, forPlayer);
        }
    }
}