using System;
using System.Collections.Concurrent;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace CoalPiles
{
    public class BlockEntityCoalPile : BlockEntity
    {
        public const int ItemsPerLayer = 16;
        public const int MaxLayers = 10;
        public const int MaxCount = ItemsPerLayer * MaxLayers; // 160
        private const float LayerHeight = 1.0f / MaxLayers; // 0.10f per layer -> 10 layers = 1.0 full block
        private const float PieceScale = 0.82f;             // Slightly tighter X/Z footprint so pieces don't overlap

        // private static readonly AssetLocation EmberTextureLoc = new AssetLocation("game:block/coal/ember"); // [FIRE DISABLED]
        private static readonly Vec3f CenterOrigin = new Vec3f(0.5f, 0f, 0.5f);
        private static readonly ConcurrentDictionary<string, MeshData> BaseMeshCache = new ConcurrentDictionary<string, MeshData>();
        private static readonly ConcurrentDictionary<string, float> BaseMeshHeightCache = new ConcurrentDictionary<string, float>();

        public string ItemCode = CoalPilesModSystem.DefaultFuelCode;
        public int Count = 1;

        /* [FIRE DISABLED]
        public bool IsBurning = false;

        private float burnProgressSeconds = 0f;
        private long serverBurnListenerId = 0;
        private long clientParticleListenerId = 0;
        */

        // 16 deterministic positions (center -> sides -> corners), staggered so every neighbor is >= 0.25 blocks apart
        private static readonly (float x, float z, float rotDeg)[] PileSlots =
        {
            (0.39f, 0.38f,  18f),
            (0.61f, 0.62f, 112f),
            (0.64f, 0.36f, 205f),
            (0.36f, 0.64f, 294f),
            (0.36f, 0.13f,  63f),
            (0.64f, 0.87f, 148f),
            (0.89f, 0.38f, 241f),
            (0.11f, 0.62f, 327f),
            (0.61f, 0.11f,  34f),
            (0.39f, 0.89f, 172f),
            (0.14f, 0.36f, 266f),
            (0.86f, 0.64f,  88f),
            (0.11f, 0.11f, 131f),
            (0.89f, 0.89f, 219f),
            (0.86f, 0.13f, 308f),
            (0.14f, 0.87f,  49f),
        };

        public static void ClearMeshCache()
        {
            BaseMeshCache.Clear();
            BaseMeshHeightCache.Clear();
        }

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);

            if (api is ICoreClientAPI capi)
            {
                EnsureBaseMesh(capi, ItemCode, false);
                // UpdateClientParticleListener(); // [FIRE DISABLED]
            }
            /* [FIRE DISABLED]
            else if (IsBurning)
            {
                RegisterServerBurnListener();
            }
            */
        }

        /* [FIRE DISABLED]
        public void TryIgnite()
        {
            if (IsBurning || Api?.Side != EnumAppSide.Server) return;

            IsBurning = true;
            burnProgressSeconds = 0f;
            RegisterServerBurnListener();
            MarkDirty(true);
            Api.World.BlockAccessor.TriggerNeighbourBlockUpdate(Pos);
        }

        public void Extinguish()
        {
            if (!IsBurning) return;

            IsBurning = false;
            UnregisterServerBurnListener();
            if (Api?.Side == EnumAppSide.Server)
            {
                Api.World.PlaySoundAt(new AssetLocation("game:effect/extinguish"), Pos.X + 0.5, Pos.Y + 0.5, Pos.Z + 0.5, null, false, 16f);
                MarkDirty(true);
            }
        }

        private void RegisterServerBurnListener()
        {
            if (serverBurnListenerId == 0 && Api?.Side == EnumAppSide.Server)
            {
                serverBurnListenerId = RegisterGameTickListener(OnServerBurnTick, 1000);
            }
        }

        private void UnregisterServerBurnListener()
        {
            if (serverBurnListenerId != 0)
            {
                UnregisterGameTickListener(serverBurnListenerId);
                serverBurnListenerId = 0;
            }
        }

        private void UpdateClientParticleListener()
        {
            if (Api?.Side != EnumAppSide.Client) return;

            if (IsBurning && clientParticleListenerId == 0)
            {
                clientParticleListenerId = RegisterGameTickListener(OnClientParticleTick, 250);
            }
            else if (!IsBurning && clientParticleListenerId != 0)
            {
                UnregisterGameTickListener(clientParticleListenerId);
                clientParticleListenerId = 0;
            }
        }

        private void OnServerBurnTick(float dt)
        {
            if (!IsBurning || Api == null) return;

            // Extinguish if submerged in liquid
            Block liquid = Api.World.BlockAccessor.GetBlock(Pos, BlockLayersAccess.Fluid);
            if (liquid != null && liquid.LiquidLevel > 0)
            {
                Extinguish();
                return;
            }

            // Spread fire to adjacent coal piles
            if (Api.World.Rand.NextDouble() < 0.15)
            {
                foreach (BlockFacing face in BlockFacing.ALLFACES)
                {
                    var neighborBe = Api.World.BlockAccessor.GetBlockEntity(Pos.AddCopy(face)) as BlockEntityCoalPile;
                    if (neighborBe != null && !neighborBe.IsBurning)
                    {
                        neighborBe.TryIgnite();
                    }
                }
            }

            // Consume fuel based on the item's native burn duration
            Item fuelItem = CoalPilesModSystem.GetFuelItem(Api.World, ItemCode);
            float burnDuration = fuelItem?.CombustibleProps?.BurnDuration ?? 40f;
            if (burnDuration <= 0f) burnDuration = 40f;

            burnProgressSeconds += dt;
            if (burnProgressSeconds >= burnDuration)
            {
                burnProgressSeconds -= burnDuration;
                bool wasFull = Count >= MaxCount;
                Count--;

                if (Count <= 0)
                {
                    UnregisterServerBurnListener();
                    Api.World.BlockAccessor.SetBlock(0, Pos);
                    return;
                }

                MarkDirty(true);
                if (wasFull)
                {
                    Api.World.BlockAccessor.TriggerNeighbourBlockUpdate(Pos);
                }
            }
        }

        private void OnClientParticleTick(float dt)
        {
            if (!IsBurning || Api == null) return;

            float topY = GameMath.Clamp(((Count - 1) / ItemsPerLayer + 1) * LayerHeight, LayerHeight, 1.0f);
            var rand = Api.World.Rand;

            SimpleParticleProperties smoke = new SimpleParticleProperties(
                1, 2,
                ColorUtil.ToRgba(90, 50, 50, 50),
                new Vec3d(Pos.X + 0.15, Pos.Y + topY, Pos.Z + 0.15),
                new Vec3d(Pos.X + 0.85, Pos.Y + topY + 0.1, Pos.Z + 0.85),
                new Vec3f(-0.1f, 0.4f, -0.1f),
                new Vec3f(0.1f, 0.9f, 0.1f),
                1.5f, 0f, 0.4f, 0.9f, EnumParticleModel.Quad
            );
            smoke.SelfPropelled = true;
            Api.World.SpawnParticles(smoke);

            if (rand.NextDouble() < 0.6)
            {
                SimpleParticleProperties ember = new SimpleParticleProperties(
                    1, 1,
                    ColorUtil.ToRgba(255, 255, 120, 20),
                    new Vec3d(Pos.X + 0.2, Pos.Y + topY, Pos.Z + 0.2),
                    new Vec3d(Pos.X + 0.8, Pos.Y + topY + 0.05, Pos.Z + 0.8),
                    new Vec3f(-0.15f, 0.6f, -0.15f),
                    new Vec3f(0.15f, 1.2f, 0.15f),
                    0.6f, 0f, 0.12f, 0.22f, EnumParticleModel.Cube
                );
                ember.VertexFlags = 180;
                Api.World.SpawnParticles(ember);
            }
        }
        */

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
            tree.SetString("itemCode", ItemCode);
            tree.SetInt("count", Count);
            /* [FIRE DISABLED]
            tree.SetBool("isBurning", IsBurning);
            tree.SetFloat("burnProgress", burnProgressSeconds);
            */
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
        {
            base.FromTreeAttributes(tree, worldAccessForResolve);
            ItemCode = tree.GetString("itemCode", CoalPilesModSystem.DefaultFuelCode);
            Count = tree.GetInt("count", 1);
            /* [FIRE DISABLED]
            IsBurning = tree.GetBool("isBurning", false);
            burnProgressSeconds = tree.GetFloat("burnProgress", 0f);
            */

            if (Api is ICoreClientAPI capi)
            {
                EnsureBaseMesh(capi, ItemCode, false);
                // UpdateClientParticleListener(); // [FIRE DISABLED]
                Api.World.BlockAccessor.MarkBlockDirty(Pos);
            }
        }

        private static string GetMeshCacheKey(string itemCode, bool isBurning = false)
        {
            // return isBurning ? itemCode + ":burning" : itemCode; // [FIRE DISABLED]
            return itemCode;
        }

        private static MeshData EnsureBaseMesh(ICoreClientAPI capi, string itemCode, bool isBurning = false)
        {
            if (string.IsNullOrEmpty(itemCode)) return null;

            string cacheKey = GetMeshCacheKey(itemCode, isBurning);
            if (BaseMeshCache.TryGetValue(cacheKey, out MeshData cached)) return cached;

            Item fuelItem = CoalPilesModSystem.GetFuelItem(capi.World, itemCode);
            if (fuelItem?.Shape?.Base == null) return null;

            AssetLocation shapeLoc = fuelItem.Shape.Base.Clone().WithPathPrefixOnce("shapes/").WithPathAppendixOnce(".json");
            Shape shape = capi.Assets.TryGet(shapeLoc)?.ToObject<Shape>();
            if (shape == null) return null;

            var texSource = new ShapeTextureSource(capi, shape, shapeLoc.ToString());
            /* [FIRE DISABLED]
            if (isBurning)
            {
                // Swap all shape & item textures to the glowing ember texture
                var emberTex = new CompositeTexture(EmberTextureLoc);
                if (shape.Textures != null)
                {
                    foreach (string key in shape.Textures.Keys.ToList())
                    {
                        texSource.textures[key] = emberTex;
                    }
                }
                if (fuelItem.Textures != null)
                {
                    foreach (string key in fuelItem.Textures.Keys)
                    {
                        texSource.textures[key] = emberTex;
                    }
                }
            }
            else
            */
            if (fuelItem.Textures != null)
            {
                foreach (var val in fuelItem.Textures)
                {
                    texSource.textures[val.Key] = val.Value;
                }
            }

            capi.Tesselator.TesselateShape("coalpile-" + cacheKey, shape, out MeshData baseMesh, texSource);
            if (baseMesh != null)
            {
                // Ground mesh cleanly at Y = 0 and measure its exact height so layers never overlap
                float minY = float.MaxValue;
                float maxY = float.MinValue;
                if (baseMesh.xyz != null && baseMesh.VerticesCount > 0)
                {
                    for (int v = 0; v < baseMesh.VerticesCount; v++)
                    {
                        float vy = baseMesh.xyz[v * 3 + 1];
                        if (vy < minY) minY = vy;
                        if (vy > maxY) maxY = vy;
                    }
                }

                if (minY < float.MaxValue && maxY > minY)
                {
                    if (Math.Abs(minY) > 0.0001f)
                    {
                        baseMesh.Translate(0f, -minY, 0f);
                    }
                    BaseMeshHeightCache[cacheKey] = maxY - minY;
                }

                /* [FIRE DISABLED]
                if (isBurning && baseMesh.Flags != null)
                {
                    // Apply vertex glow so burning coal pieces visibly glow in the dark
                    for (int i = 0; i < baseMesh.Flags.Length; i++)
                    {
                        baseMesh.Flags[i] = (baseMesh.Flags[i] & ~VertexFlags.GlowLevelBitMask) | 180;
                    }
                }
                */

                BaseMeshCache[cacheKey] = baseMesh;
            }

            return baseMesh;
        }

        public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tessThreadTesselator)
        {
            ICoreClientAPI capi = Api as ICoreClientAPI;
            if (capi == null) return true;

            string cacheKey = GetMeshCacheKey(ItemCode, false);
            if (!BaseMeshCache.TryGetValue(cacheKey, out MeshData baseMesh))
            {
                string code = ItemCode;
                // bool burning = IsBurning; // [FIRE DISABLED]
                BlockPos pos = Pos.Copy();
                capi.Event.EnqueueMainThreadTask(() =>
                {
                    if (EnsureBaseMesh(capi, code, false) != null)
                    {
                        capi.World.BlockAccessor.MarkBlockDirty(pos);
                    }
                }, "coalpiles-genmesh");
                return true;
            }

            // Cap vertical scale so each piece fits strictly inside LayerHeight (0.10f) without overlapping
            float scaleY = PieceScale;
            if (BaseMeshHeightCache.TryGetValue(cacheKey, out float meshHeight) && meshHeight > 0.0001f)
            {
                scaleY = Math.Min(PieceScale, LayerHeight / meshHeight);
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
                    .Scale(CenterOrigin, PieceScale, scaleY, PieceScale)
                    .Rotate(CenterOrigin, 0f, rotDeg * GameMath.DEG2RAD, 0f)
                    .Translate(dx, dy, dz)
                    .Rotate(CenterOrigin, 0f, layerRotY, 0f);

                mesher.AddMeshData(piece);
            }

            return true;
        }
    }
}