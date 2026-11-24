using System;
using System.Collections.Concurrent;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace CoalPiles
{
    public class CoalPilesModSystem : ModSystem
    {
        public const bool DebugChat = false;
        public const string PileBlockCode = "coalpiles:densecoalpile";
        public const string DefaultFuelCode = "charcoal";

        public static readonly string[] SupportedFuelPaths =
        {
            "charcoal",
            "ore-bituminouscoal",
            "ore-lignite",
            "ore-anthracite",
            "coke",
            "coal-contaminated"
        };

        public override void Start(ICoreAPI api)
        {
            base.Start(api);
            api.RegisterBlockClass("BlockDenseCoalPile", typeof(BlockCoalPile));
            api.RegisterBlockEntityClass("DenseCoalPile", typeof(BlockEntityCoalPile));
            api.RegisterCollectibleBehaviorClass("DenseCoalPilePlace", typeof(CoalPilePlaceBehavior));
        }

        public override void AssetsFinalize(ICoreAPI api)
        {
            base.AssetsFinalize(api);

            foreach (string path in SupportedFuelPaths)
            {
                Item item = GetFuelItem(api.World, path);
                if (item == null) continue;

                var behaviors = item.CollectibleBehaviors.ToList();
                behaviors.Insert(0, new CoalPilePlaceBehavior(item));
                item.CollectibleBehaviors = behaviors.ToArray();
            }
        }

        public override void Dispose()
        {
            base.Dispose();
            BlockEntityCoalPile.ClearMeshCache();
        }

        public static Item GetFuelItem(IWorldAccessor world, string fuelCode)
        {
            return world?.GetItem(new AssetLocation("game:" + fuelCode));
        }

        public static string GetFuelName(IWorldAccessor world, string fuelCode)
        {
            Item item = GetFuelItem(world, fuelCode);
            return item != null ? new ItemStack(item).GetName() : fuelCode;
        }

        public static void LogChat(IWorldAccessor world, string message)
        {
            if (DebugChat && world.Api is ICoreClientAPI capi)
            {
                capi.ShowChatMessage($"[CoalPiles] {message}");
            }
        }
    }

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

            // Case 1: Aiming at an existing pile -> Ctrl adds 1, Ctrl + Shift adds up to 16 (and repeats while held)
            if (clickedBlock is BlockCoalPile)
            {
                if (!byEntity.Controls.CtrlKey) return;

                // Always block vanilla ItemPileable from running on our pile
                handHandling = EnumHandHandling.PreventDefaultAction;
                handling = EnumHandling.PreventDefault;

                if (TryAddToPile(world, slot, byEntity, blockSel.Position))
                {
                    byEntity.Attributes.SetFloat(NextAddAttr, InitialHoldDelay);
                }

                return;
            }

            // Case 2: Aiming at regular ground -> Ctrl (without Shift) places 1st item and creates the pile
            if (byEntity.Controls.CtrlKey && !byEntity.Controls.ShiftKey)
            {
                BlockPos targetPos = blockSel.DidOffset ? blockSel.Position : blockSel.Position.AddCopy(blockSel.Face);
                Block targetBlock = world.BlockAccessor.GetBlock(targetPos);
                Block belowBlock = world.BlockAccessor.GetBlock(targetPos.DownCopy());

                if (!belowBlock.CanAttachBlockAt(world.BlockAccessor, belowBlock, targetPos.DownCopy(), BlockFacing.UP))
                {
                    return;
                }

                Block pileBlock = world.GetBlock(new AssetLocation(CoalPilesModSystem.PileBlockCode));
                if (pileBlock != null && targetBlock.IsReplacableBy(pileBlock))
                {
                    string itemPath = slot.Itemstack.Collectible.Code.Path;
                    string itemName = slot.Itemstack.GetName();

                    if (world.Side == EnumAppSide.Server)
                    {
                        world.BlockAccessor.SetBlock(pileBlock.BlockId, targetPos);
                        var be = world.BlockAccessor.GetBlockEntity(targetPos) as BlockEntityCoalPile;
                        if (be != null)
                        {
                            be.ItemCode = itemPath;
                            be.Count = 1;
                            be.MarkDirty(true);
                        }

                        slot.TakeOut(1);
                        slot.MarkDirty();
                    }

                    CoalPilesModSystem.LogChat(world, $"Placed {itemName} (1/{BlockEntityCoalPile.MaxCount})");

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
            return TryAddToPile(world, slot, byEntity, blockSel.Position);
        }

        private bool TryAddToPile(IWorldAccessor world, ItemSlot slot, EntityAgent byEntity, BlockPos pos)
        {
            var be = world.BlockAccessor.GetBlockEntity(pos) as BlockEntityCoalPile;
            if (be == null || slot.Itemstack.Collectible.Code.Path != be.ItemCode) return false;

            int spaceLeft = BlockEntityCoalPile.MaxCount - be.Count;
            if (spaceLeft <= 0) return false;

            int wanted = byEntity.Controls.ShiftKey ? BlockEntityCoalPile.ItemsPerLayer : 1;
            int toAdd = GameMath.Min(wanted, GameMath.Min(spaceLeft, slot.StackSize));
            if (toAdd <= 0) return false;

            string itemName = slot.Itemstack.GetName();
            int newCount = be.Count + toAdd;

            if (world.Side == EnumAppSide.Server)
            {
                slot.TakeOut(toAdd);
                slot.MarkDirty();
                be.Count = newCount;
                be.MarkDirty(true);
            }

            CoalPilesModSystem.LogChat(world, $"Added {toAdd} {itemName} ({newCount}/{BlockEntityCoalPile.MaxCount})");
            return true;
        }
    }

    public class BlockCoalPile : Block
    {
        private static readonly Cuboidf[][] LayerBoxes =
        {
            new[] { new Cuboidf(0f, 0f, 0f, 1f, 0.12f, 1f) }, // Layer 1 (1-16)
            new[] { new Cuboidf(0f, 0f, 0f, 1f, 0.22f, 1f) }, // Layer 2 (17-32)
            new[] { new Cuboidf(0f, 0f, 0f, 1f, 0.32f, 1f) }, // Layer 3 (33-48)
            new[] { new Cuboidf(0f, 0f, 0f, 1f, 0.42f, 1f) }, // Layer 4 (49-64)
            new[] { new Cuboidf(0f, 0f, 0f, 1f, 0.52f, 1f) }, // Layer 5 (65-80)
            new[] { new Cuboidf(0f, 0f, 0f, 1f, 0.62f, 1f) }, // Layer 6 (81-96)
            new[] { new Cuboidf(0f, 0f, 0f, 1f, 0.72f, 1f) }, // Layer 7 (97-112)
            new[] { new Cuboidf(0f, 0f, 0f, 1f, 0.82f, 1f) }, // Layer 8 (113-128)
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

            BlockPos supportPos = targetPos.DownCopy();
            Block supportBlock = world.BlockAccessor.GetBlock(supportPos);

            // Remove old pile
            world.BlockAccessor.SetBlock(0, pos);

            if (supportBlock.CanAttachBlockAt(world.BlockAccessor, this, supportPos, BlockFacing.UP))
            {
                // Place pile at the new position with its exact fuel type & count
                world.BlockAccessor.SetBlock(BlockId, targetPos);
                var newBe = world.BlockAccessor.GetBlockEntity(targetPos) as BlockEntityCoalPile;
                if (newBe != null)
                {
                    newBe.ItemCode = itemCode;
                    newBe.Count = count;
                    newBe.MarkDirty(true);
                }

                world.PlaySoundAt(new AssetLocation("game:block/gravel"), targetPos.X + 0.5, targetPos.Y + 0.5, targetPos.Z + 0.5, null, true, 16f);
            }
            else
            {
                // Landed on a non-solid block (torch, slab, etc.) -> drop all fuel items
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
                return $"{itemName}: {be.Count} / {BlockEntityCoalPile.MaxCount}";
            }
            return base.GetPlacedBlockInfo(world, pos, forPlayer);
        }
    }

    public class BlockEntityCoalPile : BlockEntity
    {
        public const int ItemsPerLayer = 16;
        public const int MaxLayers = 8;
        public const int MaxCount = ItemsPerLayer * MaxLayers; // 128
        private const float LayerHeight = 0.095f;
        private const float PieceScale = 0.9f;

        public string ItemCode = CoalPilesModSystem.DefaultFuelCode;
        public int Count = 1;

        private static readonly Vec3f CenterOrigin = new Vec3f(0.5f, 0f, 0.5f);
        private static readonly ConcurrentDictionary<string, MeshData> BaseMeshCache = new ConcurrentDictionary<string, MeshData>();

        // 16 deterministic positions (center -> sides -> corners), staggered so every neighbor is >= 0.25 blocks apart
        private static readonly (float x, float z, float rotDeg)[] PileSlots =
        {
            // Center 4 (1-4)
            (0.39f, 0.38f,  18f),
            (0.61f, 0.62f, 112f),
            (0.64f, 0.36f, 205f),
            (0.36f, 0.64f, 294f),

            // Expanding to the sides (5-12)
            (0.36f, 0.13f,  63f),
            (0.64f, 0.87f, 148f),
            (0.89f, 0.38f, 241f),
            (0.11f, 0.62f, 327f),
            (0.61f, 0.11f,  34f),
            (0.39f, 0.89f, 172f),
            (0.14f, 0.36f, 266f),
            (0.86f, 0.64f,  88f),

            // Outer corners (13-16)
            (0.11f, 0.11f, 131f),
            (0.89f, 0.89f, 219f),
            (0.86f, 0.13f, 308f),
            (0.14f, 0.87f,  49f),
        };

        public static void ClearMeshCache()
        {
            BaseMeshCache.Clear();
        }

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);
            if (api is ICoreClientAPI capi)
            {
                EnsureBaseMesh(capi, ItemCode);
            }
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
            tree.SetString("itemCode", ItemCode);
            tree.SetInt("count", Count);
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
        {
            base.FromTreeAttributes(tree, worldAccessForResolve);
            ItemCode = tree.GetString("itemCode", CoalPilesModSystem.DefaultFuelCode);
            Count = tree.GetInt("count", 1);

            if (Api is ICoreClientAPI capi)
            {
                EnsureBaseMesh(capi, ItemCode);
                Api.World.BlockAccessor.MarkBlockDirty(Pos);
            }
        }

        private static MeshData EnsureBaseMesh(ICoreClientAPI capi, string itemCode)
        {
            if (string.IsNullOrEmpty(itemCode)) return null;
            if (BaseMeshCache.TryGetValue(itemCode, out MeshData cached)) return cached;

            Item fuelItem = CoalPilesModSystem.GetFuelItem(capi.World, itemCode);
            if (fuelItem?.Shape?.Base == null) return null;

            AssetLocation shapeLoc = fuelItem.Shape.Base.Clone().WithPathPrefixOnce("shapes/").WithPathAppendixOnce(".json");
            Shape shape = capi.Assets.TryGet(shapeLoc)?.ToObject<Shape>();
            if (shape == null) return null;

            // Use the item's own shape + textures, inserted into the block texture atlas
            var texSource = new ShapeTextureSource(capi, shape, shapeLoc.ToString());
            if (fuelItem.Textures != null)
            {
                foreach (var val in fuelItem.Textures)
                {
                    texSource.textures[val.Key] = val.Value;
                }
            }

            capi.Tesselator.TesselateShape("coalpile-" + itemCode, shape, out MeshData baseMesh, texSource);
            if (baseMesh != null)
            {
                BaseMeshCache[itemCode] = baseMesh;
            }

            return baseMesh;
        }

        public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tessThreadTesselator)
        {
            ICoreClientAPI capi = Api as ICoreClientAPI;
            if (capi == null) return true;

            if (!BaseMeshCache.TryGetValue(ItemCode, out MeshData baseMesh))
            {
                string code = ItemCode;
                BlockPos pos = Pos.Copy();
                capi.Event.EnqueueMainThreadTask(() =>
                {
                    if (EnsureBaseMesh(capi, code) != null)
                    {
                        capi.World.BlockAccessor.MarkBlockDirty(pos);
                    }
                }, "coalpiles-genmesh");
                return true;
            }

            int renderCount = GameMath.Clamp(Count, 0, MaxCount);
            for (int i = 0; i < renderCount; i++)
            {
                int layer = i / ItemsPerLayer;
                int slotIdx = i % ItemsPerLayer;

                var (x, z, rotDeg) = PileSlots[slotIdx];
                float dx = x - 0.5f;
                float dz = z - 0.5f;
                float dy = layer * LayerHeight;
                float layerRotY = (layer * 90f) * GameMath.DEG2RAD;

                MeshData piece = baseMesh.Clone()
                    .Scale(CenterOrigin, PieceScale, PieceScale, PieceScale)
                    .Rotate(CenterOrigin, 0f, rotDeg * GameMath.DEG2RAD, 0f)
                    .Translate(dx, dy, dz)
                    .Rotate(CenterOrigin, 0f, layerRotY, 0f);

                mesher.AddMeshData(piece);
            }

            return true;
        }
    }
}