using System;
using System.Collections.Generic;
using UnityEngine;

namespace BusOut.Gameplay.Editor
{
    /// Cơ chế bật cho lần sinh này (chọn trên Level Builder). Số của từng cơ chế nằm trong profile.
    public struct GeneratorOptions
    {
        public bool HideCarColors;
        public bool HidePassengers;
        public bool AddLocks;
        public bool AddConstruction;
        public bool AddGarages;
        /// Hình cổng bãi chờ, đơn vị level (GameplayConfig × carScale).
        public float GarageBuildingOffset;
        public float GarageBuildingLength;
        public float GarageBuildingWidth;
    }

    /// Kết quả một lần sinh.
    public sealed class GeneratedLevel
    {
        public LevelData Level;
        public int Seed;
        public int FreeAtStart;
        public int MaxChainDepth;
        /// False: hết số lần thử mà chưa đạt mục tiêu của mức khó, đây là bố cục gần nhất.
        public bool MetTargets;
    }

    /// Sinh level theo một mức khó. Cùng profile + hạt giống thì ra cùng level.
    ///
    /// Đặt xe NGƯỢC thứ tự rời bãi: xe đặt sau là xe rời trước, nên chỉ cần mỗi xe mới có đường ra trống so với các xe đã đặt
    /// (nó được phép chắn xe cũ). Khi đó luôn tồn tại thứ tự ra hết bãi là đảo ngược thứ tự đặt, level không thể kẹt vòng mà
    /// không phải dò lại sau khi sinh.
    ///
    /// Cơ chế mới thêm vào Run, sau khi chọn được bố cục, kèm một cờ trong GeneratorOptions.
    public sealed class LevelGenerator
    {
        private sealed class Layout
        {
            public readonly List<VehicleData> Cars = new List<VehicleData>();
            public readonly List<CarFootprint> Footprints = new List<CarFootprint>();
            /// Chuỗi xe dài nhất đang chờ sau xe này. Xe mới không bị ai chắn nên tính cộng dồn được, không phải duyệt lại đồ thị.
            public readonly List<int> ChainBehind = new List<int>();
            /// Mỗi xe: các xe đang chắn nó.
            public readonly List<List<int>> BlockedBy = new List<List<int>>();
            public int MaxChain;

            public int FreeCount
            {
                get
                {
                    int free = 0;
                    foreach (List<int> blockers in BlockedBy)
                        if (blockers.Count == 0) free++;
                    return free;
                }
            }
        }

        private struct Candidate
        {
            public VehicleData Car;
            public CarFootprint Footprint;
        }

        private readonly LevelGeneratorProfile profile;
        private readonly CarShape[] shapes;
        private readonly Vector2 frameMin;
        private readonly Vector2 frameMax;
        private readonly float sideInset;
        private readonly int paletteSize;
        private readonly System.Random random;
        private readonly GeneratorOptions options;

        private LevelGenerator(LevelGeneratorProfile profile, CarShape[] shapes, Vector2 frameMin, Vector2 frameMax,
            float sideInset, int paletteSize, int seed, GeneratorOptions options)
        {
            this.profile = profile;
            this.options = options;
            this.shapes = shapes;
            this.frameMin = frameMin;
            this.frameMax = frameMax;
            this.sideInset = sideInset;
            this.paletteSize = Math.Max(1, paletteSize);
            random = new System.Random(seed);
        }

        public static GeneratedLevel Generate(LevelGeneratorProfile profile, CarShape[] shapes, Vector2 frameMin, Vector2 frameMax,
            float sideInset, int paletteSize, int seed, GeneratorOptions options = default)
        {
            var generator = new LevelGenerator(profile, shapes, frameMin, frameMax, sideInset, paletteSize, seed, options);
            return generator.Run(seed);
        }

        private GeneratedLevel Run(int seed)
        {
            Layout best = null;
            int bestPenalty = int.MaxValue;
            for (int attempt = 0; attempt < Math.Max(1, profile.layoutAttempts) && bestPenalty > 0; attempt++)
            {
                int target = RandomRange(profile.carCountMin, profile.carCountMax);
                Layout layout = BuildLayout(target);
                // Gán màu ngay trong vòng thử: số màu của các xe ra được ngay cũng là một mục tiêu.
                AssignColors(layout);
                int penalty = PenaltyOf(layout, target);
                if (penalty >= bestPenalty) continue;
                best = layout;
                bestPenalty = penalty;
            }

            if (options.HideCarColors) HideBlockedCarColors(best);
            if (options.AddLocks) AddLockPairs(best);
            if (options.AddGarages) AddGarages(best);
            var level = new LevelData
            {
                seed = seed,
                difficulty = profile.displayName,
                vehicles = best.Cars.ToArray(),
                mix = CloneMix(profile.mix),
                hardPoints = CloneHardPoints(profile.hardPoints),
                passengersHidden = options.HidePassengers,
                constructionSlots = options.AddConstruction ? profile.constructionSlotCount : 0,
                constructionPassengers = options.AddConstruction ? profile.constructionPassengers : 0,
            };
            return new GeneratedLevel
            {
                Level = level,
                Seed = seed,
                FreeAtStart = best.FreeCount,
                MaxChainDepth = best.MaxChain,
                MetTargets = bestPenalty == 0,
            };
        }

        private int PenaltyOf(Layout layout, int target)
        {
            int penalty = Math.Max(0, target - layout.Cars.Count) * 4;
            penalty += Math.Max(0, profile.minChainDepth - layout.MaxChain) * 6;
            int free = layout.FreeCount;
            penalty += Math.Max(0, profile.minFreeCars - free) * 6;
            penalty += Math.Max(0, free - Mathf.CeilToInt(profile.maxFreeRatio * layout.Cars.Count));
            penalty += Math.Max(0, profile.minFreeColors - CountFreeColors(layout)) * 6;
            return penalty;
        }

        /// Chỉ ẩn xe đang bị chặn: xe ra được ngay sẽ lộ màu ngay khi vào ván, ẩn cũng vô ích.
        private void HideBlockedCarColors(Layout layout)
        {
            for (int i = 0; i < layout.Cars.Count; i++)
                if (layout.BlockedBy[i].Count > 0 && random.NextDouble() < profile.hiddenCarChance) layout.Cars[i].colorHidden = true;
        }

        /// ⚠️ Xe bị khoá luôn là xe đặt TRƯỚC xe mang chìa (đặt trước = rời bãi sau). Mọi cạnh chặn đều đi từ xe đặt trước sang xe
        /// đặt sau, cạnh khoá cũng vậy nên không thể tạo vòng. Chọn ngược chiều là có thể sinh level kẹt.
        private void AddLockPairs(Layout layout)
        {
            int count = layout.Cars.Count;
            if (count < 2) return;
            var used = new HashSet<int>();
            int pairId = 1;
            for (int attempt = 0; attempt < 80 && pairId <= profile.lockPairCount; attempt++)
            {
                int locked = random.Next(count - 1);
                int key = random.Next(locked + 1, count);
                if (used.Contains(locked) || used.Contains(key)) continue;
                // Nửa số lần thử đầu chỉ nhận xe bị khoá đang ra được ngay: khoá mới là lý do duy nhất nó phải chờ, người chơi thấy rõ.
                if (attempt < 40 && layout.BlockedBy[locked].Count > 0) continue;
                layout.Cars[locked].lockId = pairId;
                layout.Cars[key].keyId = pairId;
                used.Add(locked);
                used.Add(key);
                pairId++;
            }
        }

        /// Biến vài xe có chỗ trống phía sau thành cửa bãi chờ, thêm xe chờ bên trong. Chạy SAU khoá / chìa: xe chờ không bị gán khoá.
        /// Vẫn không kẹt vòng: xe chờ chỉ bị chắn bởi đúng những xe chắn xe đầu (đều đặt sau xe đầu) và xe trước nó trong bãi; xe bị xe đầu
        /// chắn thì đặt trước xe đầu. Mọi cạnh vẫn đi từ xe đặt trước sang xe đặt sau / xe chờ, không cạnh nào quay ngược.
        private void AddGarages(Layout layout)
        {
            int made = 0;
            var used = new HashSet<int>();
            for (int attempt = 0; attempt < 60 && made < profile.garageCount; attempt++)
            {
                int front = random.Next(layout.Cars.Count);
                VehicleData frontCar = layout.Cars[front];
                if (used.Contains(front) || frontCar.lockId > 0 || frontCar.keyId > 0) continue;
                CarFootprint building = LevelBuilderRules.BuildGarageBuilding(frontCar.x, frontCar.z, frontCar.rotY,
                    options.GarageBuildingOffset, options.GarageBuildingLength, options.GarageBuildingWidth);
                if (!LevelBuilderRules.IsInsideFrame(building, frameMin, frameMax) || BuildingConflicts(layout, building, front)) continue;

                made++;
                used.Add(front);
                frontCar.garageId = made;
                frontCar.garageOrder = 0;
                int waiting = RandomRange(profile.garageQueueMin, profile.garageQueueMax);
                for (int k = 1; k <= waiting; k++)
                {
                    // ⚠️ tool phải là -1, không thì game bỏ qua xe.
                    layout.Cars.Add(new VehicleData
                    {
                        type = PickType(), color = layout.Cars[random.Next(layout.Cars.Count)].color,
                        x = frontCar.x, z = frontCar.z, rotY = frontCar.rotY, tool = -1, garageId = made, garageOrder = k,
                    });
                }
            }
        }

        /// Cổng chồng lên xe khác, hoặc nằm trên đường ra của xe khác (cổng không có collider: xe sẽ chạy xuyên qua).
        private bool BuildingConflicts(Layout layout, CarFootprint building, int front)
        {
            for (int i = 0; i < layout.Footprints.Count; i++)
            {
                if (i == front) continue;
                CarFootprint car = layout.Footprints[i];
                CarFootprint spaced = car;
                spaced.HalfWidth += profile.carGap * 0.5f;
                spaced.HalfLength += profile.carGap * 0.5f;
                if (LevelBuilderRules.Overlaps(spaced, building) || LevelBuilderRules.PathHits(car, building, sideInset)) return true;
            }
            return false;
        }

        private static int CountFreeColors(Layout layout)
        {
            var colors = new HashSet<int>();
            for (int i = 0; i < layout.Cars.Count; i++)
                if (layout.BlockedBy[i].Count == 0) colors.Add(layout.Cars[i].color);
            return colors.Count;
        }

        #region Bố cục

        private Layout BuildLayout(int target)
        {
            var layout = new Layout();
            bool mirrored = random.NextDouble() < profile.mirrorChance;
            while (layout.Cars.Count < target)
            {
                bool placePair = mirrored && layout.Cars.Count + 1 < target;
                if (!TryPlaceNext(layout, placePair)) break;
            }
            return layout;
        }

        private bool TryPlaceNext(Layout layout, bool placePair)
        {
            bool wantsToBlock = random.NextDouble() < profile.blockChance;
            bool found = false;
            double bestScore = double.MinValue;
            Candidate bestFirst = default, bestMirror = default;

            for (int i = 0; i < Math.Max(1, profile.candidatesPerCar); i++)
            {
                Candidate first = NextCandidate(layout);
                if (!TryEvaluate(layout, first.Footprint, null, 0, out int blocks, out int freedLost, out int chain)) continue;
                double score = ScoreOf(blocks, freedLost, chain, wantsToBlock);

                Candidate mirror = default;
                if (placePair)
                {
                    mirror = MirrorOf(first.Car);
                    if (!TryEvaluate(layout, mirror.Footprint, first.Footprint, chain, out int mirrorBlocks, out int mirrorFreed, out int mirrorChain))
                        continue;
                    score += ScoreOf(mirrorBlocks, mirrorFreed, mirrorChain, wantsToBlock);
                }
                if (found && score <= bestScore) continue;
                found = true;
                bestScore = score;
                bestFirst = first;
                bestMirror = mirror;
            }
            if (!found) return false;
            Commit(layout, bestFirst);
            if (placePair) Commit(layout, bestMirror);
            return true;
        }

        private double ScoreOf(int blocks, int freedLost, int chain, bool wantsToBlock)
        {
            double value = freedLost * 2 + blocks + chain;
            // Nhiễu nhỏ để các vị trí ngang điểm không luôn ra cùng một kiểu bố cục.
            return (wantsToBlock ? value : -value) + random.NextDouble() * 0.5;
        }

        /// Xe mới hợp lệ: trong khung, không chạm xe nào, đường ra trống, không làm chuỗi chặn dài quá mức.
        /// <paramref name="extra"/>: xe cùng cặp đối xứng chưa đưa vào bố cục.
        private bool TryEvaluate(Layout layout, CarFootprint candidate, CarFootprint? extra, int extraChain,
            out int blocks, out int freedLost, out int chain)
        {
            blocks = freedLost = chain = 0;
            if (!LevelBuilderRules.IsInsideFrame(candidate, frameMin, frameMax)) return false;
            CarFootprint spaced = candidate;
            spaced.HalfWidth += profile.carGap * 0.5f;
            spaced.HalfLength += profile.carGap * 0.5f;

            for (int i = 0; i < layout.Footprints.Count; i++)
            {
                CarFootprint other = layout.Footprints[i];
                if (LevelBuilderRules.Overlaps(spaced, other) || LevelBuilderRules.PathHits(candidate, other, sideInset)) return false;
                if (!LevelBuilderRules.PathHits(other, candidate, sideInset)) continue;
                blocks++;
                if (layout.BlockedBy[i].Count == 0) freedLost++;
                chain = Math.Max(chain, layout.ChainBehind[i] + 1);
            }
            if (extra.HasValue)
            {
                if (LevelBuilderRules.Overlaps(spaced, extra.Value) || LevelBuilderRules.PathHits(candidate, extra.Value, sideInset)) return false;
                if (LevelBuilderRules.PathHits(extra.Value, candidate, sideInset)) chain = Math.Max(chain, extraChain + 1);
            }
            return chain <= profile.maxChainDepth;
        }

        private void Commit(Layout layout, Candidate candidate)
        {
            int newIndex = layout.Cars.Count;
            int chain = 0;
            for (int i = 0; i < newIndex; i++)
            {
                if (!LevelBuilderRules.PathHits(layout.Footprints[i], candidate.Footprint, sideInset)) continue;
                layout.BlockedBy[i].Add(newIndex);
                chain = Math.Max(chain, layout.ChainBehind[i] + 1);
            }
            layout.Cars.Add(candidate.Car);
            layout.Footprints.Add(candidate.Footprint);
            layout.ChainBehind.Add(chain);
            layout.BlockedBy.Add(new List<int>());
            layout.MaxChain = Math.Max(layout.MaxChain, chain);
        }

        private Candidate NextCandidate(Layout layout)
        {
            int type = PickType();
            CarShape shape = shapes[type];
            float angle = PickAngle();
            Vector2 center;

            if (layout.Cars.Count == 0)
            {
                // Xe đặt đầu là xe rời cuối: nằm giữa bãi thì các xe sau mới có chỗ bọc quanh và chắn nó.
                center = (frameMin + frameMax) * 0.5f + new Vector2(RandomFloat(-2f, 2f), RandomFloat(-2f, 2f));
            }
            else if (random.NextDouble() < profile.alignChance)
            {
                int anchorIndex = random.Next(layout.Cars.Count);
                CarFootprint anchor = layout.Footprints[anchorIndex];
                angle = layout.Cars[anchorIndex].rotY;
                if (random.NextDouble() < 0.7)
                {
                    float side = random.Next(2) == 0 ? -1f : 1f;
                    center = anchor.Center + anchor.Right * (side * (anchor.HalfWidth + shape.HalfWidth + profile.carGap * 1.05f));
                    // Thẳng hàng đầu xe để nhìn như một dãy, xe dài ngắn lẫn lộn vẫn gọn.
                    center += anchor.Forward * (anchor.HalfLength - shape.HalfLength);
                }
                else center = anchor.Center + anchor.Forward * (anchor.HalfLength + shape.HalfLength + profile.carGap * 1.05f);
            }
            else
            {
                float step = Mathf.Max(0.05f, profile.gridStep);
                center = new Vector2(Mathf.Round(RandomFloat(frameMin.x, frameMax.x) / step) * step,
                    Mathf.Round(RandomFloat(frameMin.y, frameMax.y) / step) * step);
            }
            return CreateCandidate(type, angle, center);
        }

        private Candidate CreateCandidate(int type, float angle, Vector2 bodyCenter)
        {
            CarShape shape = shapes[type];
            CarFootprint direction = LevelBuilderRules.BuildFootprint(0f, 0f, angle, default);
            Vector2 pivot = bodyCenter - direction.Right * shape.CenterRight - direction.Forward * shape.CenterForward;
            return CreateAtPivot(type, angle, Mathf.Round(pivot.x * 100f) / 100f, Mathf.Round(pivot.y * 100f) / 100f);
        }

        private Candidate CreateAtPivot(int type, float angle, float x, float z)
        {
            // ⚠️ tool phải là -1 (đỗ tự do): để mặc định 0 thì game bỏ qua không spawn.
            var car = new VehicleData { type = type, x = x, z = z, rotY = angle, tool = -1 };
            return new Candidate { Car = car, Footprint = LevelBuilderRules.BuildFootprint(x, z, angle, shapes[type]) };
        }

        private Candidate MirrorOf(VehicleData car)
        {
            float centerX = (frameMin.x + frameMax.x) * 0.5f;
            // Đảo qua trục dọc: góc đổi dấu. Không tính từ tâm thân: tâm thân lệch theo hướng xe, gốc xe đảo trực tiếp mới đúng.
            return CreateAtPivot(car.type, Mathf.Repeat(-car.rotY, 360f), 2f * centerX - car.x, car.z);
        }

        private int PickType()
        {
            float[] weights = { profile.weightFourSeat, profile.weightSixSeat, profile.weightTenSeat };
            float total = weights[0] + weights[1] + weights[2];
            double roll = random.NextDouble() * Math.Max(0.0001f, total);
            for (int i = 0; i < weights.Length; i++)
            {
                roll -= weights[i];
                if (roll < 0) return i;
            }
            return 0;
        }

        private float PickAngle()
        {
            float angle = random.Next(4) * 90f;
            return random.NextDouble() < profile.diagonalChance ? angle + 45f : angle;
        }

        #endregion

        #region Màu

        /// Chia đều số ghế giữa các màu; xe cùng màu hay khác màu với xe chắn nó tuỳ mức khó.
        private void AssignColors(Layout layout)
        {
            int colorCount = Math.Min(paletteSize, RandomRange(profile.colorCountMin, profile.colorCountMax));
            var palette = new List<int>();
            for (int i = 0; i < paletteSize; i++) palette.Add(i);
            for (int i = 0; i < colorCount; i++)
            {
                int swap = random.Next(i, palette.Count);
                (palette[i], palette[swap]) = (palette[swap], palette[i]);
            }

            var seatTotals = new int[colorCount];
            var slotOfCar = new int[layout.Cars.Count];
            // Duyệt từ xe đặt sau: xe chắn được gán màu trước xe bị chắn.
            for (int i = layout.Cars.Count - 1; i >= 0; i--)
            {
                int blockerSlot = layout.BlockedBy[i].Count > 0 ? slotOfCar[layout.BlockedBy[i][random.Next(layout.BlockedBy[i].Count)]] : -1;
                int slot = blockerSlot >= 0 && random.NextDouble() < profile.sameColorAsBlockerChance
                    ? blockerSlot
                    : LowestSeatSlot(seatTotals, blockerSlot);
                slotOfCar[i] = slot;
                seatTotals[slot] += CarModel.SeatsOf((VehicleType)layout.Cars[i].type);
                layout.Cars[i].color = palette[slot];
            }
        }

        private int LowestSeatSlot(int[] seatTotals, int avoidSlot)
        {
            int best = -1;
            for (int i = 0; i < seatTotals.Length; i++)
            {
                if (i == avoidSlot && seatTotals.Length > 1) continue;
                if (best < 0 || seatTotals[i] < seatTotals[best]) best = i;
            }
            return best;
        }

        #endregion

        private int RandomRange(int min, int max) => max <= min ? min : random.Next(min, max + 1);

        private float RandomFloat(float min, float max) => min + (float)random.NextDouble() * (max - min);

        // ⚠️ Phải chép: dùng chung object với config thì sửa dòng mix trên cửa sổ sẽ đổi luôn số trong asset.
        private static MixData[] CloneMix(MixData[] source)
        {
            if (source == null) return new MixData[0];
            var copy = new MixData[source.Length];
            for (int i = 0; i < source.Length; i++)
                copy[i] = new MixData { at = source[i].at, size = source[i].size, from = source[i].from, to = source[i].to };
            return copy;
        }

        private static HardPointData[] CloneHardPoints(HardPointData[] source)
        {
            if (source == null) return new HardPointData[0];
            var copy = new HardPointData[source.Length];
            for (int i = 0; i < source.Length; i++)
                copy[i] = new HardPointData { at = source[i].at, bonus = source[i].bonus, series = source[i].series };
            return copy;
        }
    }
}
