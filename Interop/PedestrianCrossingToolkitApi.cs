using System;
using System.Collections.Generic;
using UnityEngine;

namespace PedestrianCrossingToolkit
{
    /// <summary>
    /// Stable public boundary for coordinating road replacements with PCT-owned crossings.
    /// Network methods are invoked from the simulation thread. Unity visual work is
    /// completed asynchronously on the main thread and reported through transaction status.
    /// </summary>
    public static class PedestrianCrossingToolkitApi
    {
        public const int ApiVersion = 2;
        public const int RoadReplacementRemovingVisuals = 1;
        public const int RoadReplacementReadyForNetworkReplacement = 2;
        public const int RoadReplacementRebuildingVisuals = 3;
        public const int RoadReplacementCompleted = 4;
        public const int RoadReplacementFailed = 5;
        public const int RoadReplacementSegmentBatchLimit = 1;

        private sealed class RoadReplacementTransaction
        {
            public readonly int Id;
            public readonly CrossingPlacementAsset[] Assets;
            public readonly int[] AssetIds;
            public volatile int Phase;
            public int RestoredCount;
            public int FailedCount;
            public string Message;

            public RoadReplacementTransaction(int id, CrossingPlacementAsset[] assets)
            {
                Id = id;
                Assets = assets ?? new CrossingPlacementAsset[0];
                AssetIds = new int[Assets.Length];
                for (int i = 0; i < Assets.Length; i++)
                    AssetIds[i] = Assets[i].Id;
                Phase = RoadReplacementRemovingVisuals;
                Message = string.Empty;
            }
        }

        private static readonly Dictionary<int, RoadReplacementTransaction> RoadReplacementTransactions =
            new Dictionary<int, RoadReplacementTransaction>();
        private static int _nextRoadReplacementTransactionId = 1;

        public static int GetRegisteredCrossingCount()
        {
            return CrossingPlacementRegistry.Count;
        }

        public static int GetRoadReplacementSegmentBatchLimit()
        {
            return RoadReplacementSegmentBatchLimit;
        }

        public static int CopyTouchedRoadSegmentIds(ushort[] destination)
        {
            var touched = new HashSet<ushort>();
            CrossingPlacementAsset[] assets =
                new CrossingPlacementAsset[CrossingPlacementRegistry.Count];
            int assetCount = CrossingPlacementRegistry.CopyTo(assets);
            NetManager netManager = NetManager.instance;

            for (int i = 0; i < assetCount; i++)
            {
                CrossingPlacementAsset asset = assets[i];
                CrossingPlacementAsset liveAsset;
                string rebindError;
                if (asset.Id == 0
                    || !PedestrianCrossingToolkitState.TryRebindAssetForRoadReplacement(
                        asset,
                        out liveAsset,
                        out rebindError))
                {
                    continue;
                }

                AddTouchedRoadSegment(touched, liveAsset.Placement.SegmentId, netManager);
                if (liveAsset.Placement.HasSecondaryPoint)
                    AddTouchedRoadSegment(touched, liveAsset.Placement.SecondarySegmentId, netManager);

                ushort targetNodeId = liveAsset.Plan.TargetNodeId != 0
                    ? liveAsset.Plan.TargetNodeId
                    : liveAsset.Placement.TargetNodeId;
                AddTouchedRoadNodeSegments(touched, targetNodeId, netManager);

                ushort secondaryTargetNodeId = liveAsset.Placement.SecondaryTargetNodeId;
                AddTouchedRoadNodeSegments(touched, secondaryTargetNodeId, netManager);
            }

            int required = touched.Count;
            if (destination == null || destination.Length == 0)
                return required;

            int written = 0;
            foreach (ushort segmentId in touched)
            {
                if (written >= destination.Length)
                    break;
                destination[written++] = segmentId;
            }

            return required;
        }

        public static bool BeginRoadReplacement(
            ushort[] segmentIds,
            out int transactionId,
            out int crossingCount,
            out string message)
        {
            transactionId = 0;
            crossingCount = 0;
            message = string.Empty;

            string validationError;
            if (!ValidateSegmentIds(segmentIds, out validationError))
            {
                message = validationError;
                return false;
            }

            CrossingPlacementAsset[] registryAssets =
                new CrossingPlacementAsset[CrossingPlacementRegistry.Count];
            int registryCount = CrossingPlacementRegistry.CopyTo(registryAssets);
            var affected = new List<CrossingPlacementAsset>();
            for (int i = 0; i < registryCount; i++)
            {
                CrossingPlacementAsset asset = registryAssets[i];
                CrossingPlacementAsset liveAsset;
                string rebindError;
                if (asset.Id == 0
                    || !PedestrianCrossingToolkitState.TryRebindAssetForRoadReplacement(
                        asset,
                        out liveAsset,
                        out rebindError))
                {
                    continue;
                }

                if (TouchesAnySegment(liveAsset, segmentIds))
                    affected.Add(liveAsset);
            }

            if (affected.Count == 0)
            {
                message = "No PCT crossings touch the requested road replacement.";
                return true;
            }

            if (segmentIds.Length > RoadReplacementSegmentBatchLimit)
            {
                message = "PCT accepts at most " + RoadReplacementSegmentBatchLimit +
                          " crossing-bearing road segment per replacement transaction; " +
                          "split this request before retrying.";
                return false;
            }

            var detached = new List<CrossingPlacementAsset>(affected.Count);
            CrossingPathBuilder.BeginNetworkOnlyBuild();
            try
            {
                for (int i = 0; i < affected.Count; i++)
                {
                    CrossingPlacementAsset removed;
                    if (!PedestrianCrossingToolkitState.TryDetachAssetForRoadReplacement(
                        affected[i].Id,
                        out removed))
                    {
                        int[] restoredAssetIds = RestoreDetachedAssets(
                            detached,
                            new ushort[0],
                            new ushort[0]);
                        PedestrianCrossingToolkitState.CompleteRoadReplacementRestore(
                            restoredAssetIds,
                            restoredAssetIds.Length,
                            "api-begin-rollback");
                        PedestrianCrossingToolkitThreading.QueueMainThreadAction(
                            PedestrianCrossingToolkitPanel.RefreshInstance);
                        message = "PCT could not detach every affected crossing before the road replacement.";
                        return false;
                    }

                    detached.Add(affected[i]);
                }
            }
            finally
            {
                CrossingPathBuilder.EndNetworkOnlyBuild();
            }

            PedestrianCrossingToolkitState.CompleteRoadReplacementDetach(
                detached.Count,
                "api-road-replacement-begin");

            transactionId = AllocateRoadReplacementTransactionId();
            crossingCount = detached.Count;
            RoadReplacementTransaction transaction =
                new RoadReplacementTransaction(transactionId, detached.ToArray());
            RoadReplacementTransactions[transactionId] = transaction;
            QueueVisualRemoval(transaction);
            message = "PCT detached " + crossingCount + " crossing" +
                      (crossingCount == 1 ? string.Empty : "s") +
                      " for road replacement.";
            Debug.Log("[PedestrianCrossingToolkit] API road replacement began: transaction="
                      + transactionId
                      + " segments="
                      + segmentIds.Length
                      + " crossings="
                      + crossingCount);
            return true;
        }

        public static bool CompleteRoadReplacement(
            int transactionId,
            ushort[] originalSegmentIds,
            ushort[] replacementSegmentIds,
            out int restoredCount,
            out int failedCount,
            out string message)
        {
            restoredCount = 0;
            failedCount = 0;
            message = string.Empty;

            RoadReplacementTransaction transaction;
            if (transactionId <= 0 ||
                !RoadReplacementTransactions.TryGetValue(transactionId, out transaction))
            {
                message = "PCT road-replacement transaction was not found.";
                return false;
            }

            if (originalSegmentIds == null || replacementSegmentIds == null ||
                originalSegmentIds.Length != replacementSegmentIds.Length)
            {
                message = "PCT road-replacement segment mapping is invalid.";
                return false;
            }

            int[] restoredAssetIds = new int[transaction.Assets.Length];
            CrossingPathBuilder.BeginNetworkOnlyBuild();
            try
            {
                if (transaction.Phase != RoadReplacementReadyForNetworkReplacement)
                {
                    message = "PCT has not finished removing the affected crossing visuals.";
                    return false;
                }

                for (int i = 0; i < transaction.Assets.Length; i++)
                {
                    int restoredAssetId;
                    string restoreError;
                    if (PedestrianCrossingToolkitState.TryRestoreAssetAfterRoadReplacement(
                        transaction.Assets[i],
                        originalSegmentIds,
                        replacementSegmentIds,
                        out restoredAssetId,
                        out restoreError))
                    {
                        restoredAssetIds[restoredCount++] = restoredAssetId;
                    }
                    else
                    {
                        failedCount++;
                        Debug.LogWarning("[PedestrianCrossingToolkit] API crossing restore failed: transaction="
                                         + transactionId
                                         + " formerAsset="
                                         + transaction.Assets[i].Id
                                         + " reason="
                                         + restoreError);
                    }
                }

                int[] completedAssetIds = new int[restoredCount];
                Array.Copy(restoredAssetIds, completedAssetIds, restoredCount);
                PedestrianCrossingToolkitState.CompleteRoadReplacementRestore(
                    restoredAssetIds,
                    restoredCount,
                    "api-road-replacement-complete");
                restoredAssetIds = completedAssetIds;

                for (int i = 0; i < restoredCount; i++)
                {
                    string networkError;
                    if (CrossingPathBuilder.TryValidateRoadReplacementNetworkAsset(
                        restoredAssetIds[i],
                        out networkError))
                    {
                        continue;
                    }

                    failedCount++;
                    Debug.LogWarning("[PedestrianCrossingToolkit] API crossing network validation failed: transaction="
                                     + transactionId
                                     + " asset="
                                     + restoredAssetIds[i]
                                     + " reason="
                                     + networkError);
                }
            }
            finally
            {
                CrossingPathBuilder.EndNetworkOnlyBuild();
            }

            message = failedCount == 0
                ? "PCT restored " + restoredCount + " crossing" +
                  (restoredCount == 1 ? string.Empty : "s") + "."
                : "PCT restored " + restoredCount + " crossing" +
                  (restoredCount == 1 ? string.Empty : "s") +
                  " but could not safely restore " + failedCount + ".";
            transaction.RestoredCount = restoredCount;
            transaction.FailedCount = failedCount;
            transaction.Message = message;
            transaction.Phase = RoadReplacementRebuildingVisuals;
            QueueVisualRebuild(transaction, restoredAssetIds, restoredCount);
            Debug.Log("[PedestrianCrossingToolkit] API road replacement network restore accepted: transaction="
                      + transactionId
                      + " restored="
                      + restoredCount
                      + " failed="
                      + failedCount
                      + " mappings="
                      + originalSegmentIds.Length);
            return true;
        }

        public static bool GetRoadReplacementStatus(
            int transactionId,
            out int phase,
            out int restoredCount,
            out int failedCount,
            out string message)
        {
            phase = 0;
            restoredCount = 0;
            failedCount = 0;
            message = string.Empty;

            RoadReplacementTransaction transaction;
            if (transactionId <= 0 ||
                !RoadReplacementTransactions.TryGetValue(transactionId, out transaction))
            {
                message = "PCT road-replacement transaction was not found.";
                return false;
            }

            phase = transaction.Phase;
            restoredCount = transaction.RestoredCount;
            failedCount = transaction.FailedCount;
            message = transaction.Message ?? string.Empty;
            if (phase == RoadReplacementCompleted || phase == RoadReplacementFailed)
                RoadReplacementTransactions.Remove(transactionId);
            return true;
        }

        private static bool ValidateSegmentIds(ushort[] segmentIds, out string error)
        {
            error = string.Empty;
            if (segmentIds == null || segmentIds.Length == 0)
            {
                error = "PCT road replacement requires at least one segment ID.";
                return false;
            }

            NetManager netManager = NetManager.instance;
            if (netManager == null)
            {
                error = "The road network is unavailable.";
                return false;
            }

            for (int i = 0; i < segmentIds.Length; i++)
            {
                ushort segmentId = segmentIds[i];
                if (segmentId == 0 || segmentId >= netManager.m_segments.m_size)
                {
                    error = "PCT road replacement contains an invalid segment ID.";
                    return false;
                }

                ref NetSegment segment = ref netManager.m_segments.m_buffer[segmentId];
                if ((segment.m_flags & NetSegment.Flags.Created) == 0)
                {
                    error = "PCT road replacement contains a released segment.";
                    return false;
                }
            }

            return true;
        }

        private static bool TouchesAnySegment(CrossingPlacementAsset asset, ushort[] segmentIds)
        {
            for (int i = 0; i < segmentIds.Length; i++)
            {
                if (CrossingPlacementRegistry.IsAssetTouchingSegment(asset, segmentIds[i]))
                    return true;
            }

            return false;
        }

        private static void AddTouchedRoadNodeSegments(
            HashSet<ushort> touched,
            ushort nodeId,
            NetManager netManager)
        {
            if (touched == null
                || netManager == null
                || nodeId == 0
                || nodeId >= netManager.m_nodes.m_size)
            {
                return;
            }

            ref NetNode node = ref netManager.m_nodes.m_buffer[nodeId];
            if ((node.m_flags & NetNode.Flags.Created) == 0)
                return;

            for (int i = 0; i < 8; i++)
                AddTouchedRoadSegment(touched, node.GetSegment(i), netManager);
        }

        private static void AddTouchedRoadSegment(
            HashSet<ushort> touched,
            ushort segmentId,
            NetManager netManager)
        {
            if (touched == null
                || netManager == null
                || segmentId == 0
                || segmentId >= netManager.m_segments.m_size)
            {
                return;
            }

            ref NetSegment segment = ref netManager.m_segments.m_buffer[segmentId];
            if ((segment.m_flags & NetSegment.Flags.Created) == 0
                || segment.Info == null
                || !(segment.Info.m_netAI is RoadBaseAI))
            {
                return;
            }

            touched.Add(segmentId);
        }

        internal static void ResetForLevelChange()
        {
            foreach (RoadReplacementTransaction transaction in RoadReplacementTransactions.Values)
            {
                transaction.Message = "The city changed before PCT completed the road replacement.";
                transaction.Phase = RoadReplacementFailed;
            }
            RoadReplacementTransactions.Clear();
            _nextRoadReplacementTransactionId = 1;
        }

        private static void QueueVisualRemoval(RoadReplacementTransaction transaction)
        {
            PedestrianCrossingToolkitThreading.QueueMainThreadAction(() =>
            {
                try
                {
                    if (!PedestrianCrossingToolkitState.Enabled)
                        throw new InvalidOperationException("PCT is no longer active in this city.");

                    CrossingPathBuilder.RemoveUnityVisualsForAssets(
                        transaction.AssetIds,
                        transaction.AssetIds.Length);
                    PedestrianCrossingToolkitPanel.RefreshInstance();
                    transaction.Message = "PCT removed the affected crossing visuals.";
                    transaction.Phase = RoadReplacementReadyForNetworkReplacement;
                    Debug.Log("[PedestrianCrossingToolkit] API road replacement visual detach completed: transaction="
                              + transaction.Id
                              + " assets="
                              + transaction.AssetIds.Length);
                }
                catch (Exception e)
                {
                    transaction.FailedCount = transaction.Assets.Length;
                    transaction.Message = "PCT could not remove the affected crossing visuals: " +
                                          e.GetType().Name + ": " + e.Message;
                    transaction.Phase = RoadReplacementFailed;
                    Debug.LogError("[PedestrianCrossingToolkit] API road replacement visual detach failed: transaction="
                                   + transaction.Id
                                   + " error="
                                   + e);
                }
            });
        }

        private static void QueueVisualRebuild(
            RoadReplacementTransaction transaction,
            int[] restoredAssetIds,
            int restoredCount)
        {
            PedestrianCrossingToolkitThreading.QueueMainThreadAction(() =>
            {
                try
                {
                    if (!PedestrianCrossingToolkitState.Enabled)
                        throw new InvalidOperationException("PCT is no longer active in this city.");

                    PedestrianCrossingToolkitState.CompleteRoadReplacementVisuals(
                        restoredAssetIds,
                        restoredCount,
                        "api-road-replacement-complete");
                    transaction.Phase = transaction.FailedCount == 0
                        ? RoadReplacementCompleted
                        : RoadReplacementFailed;
                    Debug.Log("[PedestrianCrossingToolkit] API road replacement completed: transaction="
                              + transaction.Id
                              + " restored="
                              + transaction.RestoredCount
                              + " failed="
                              + transaction.FailedCount);
                }
                catch (Exception e)
                {
                    transaction.FailedCount += Math.Max(1, restoredCount);
                    transaction.Message = "PCT could not rebuild the restored crossing visuals: " +
                                          e.GetType().Name + ": " + e.Message;
                    transaction.Phase = RoadReplacementFailed;
                    Debug.LogError("[PedestrianCrossingToolkit] API road replacement visual restore failed: transaction="
                                   + transaction.Id
                                   + " error="
                                   + e);
                }
            });
        }

        private static int[] RestoreDetachedAssets(
            List<CrossingPlacementAsset> assets,
            ushort[] originalSegmentIds,
            ushort[] replacementSegmentIds)
        {
            if (assets == null)
                return new int[0];

            var restoredAssetIds = new List<int>(assets.Count);
            for (int i = 0; i < assets.Count; i++)
            {
                int restoredAssetId;
                string restoreError;
                if (PedestrianCrossingToolkitState.TryRestoreAssetAfterRoadReplacement(
                    assets[i],
                    originalSegmentIds,
                    replacementSegmentIds,
                    out restoredAssetId,
                    out restoreError))
                {
                    restoredAssetIds.Add(restoredAssetId);
                }
            }

            return restoredAssetIds.ToArray();
        }

        private static int AllocateRoadReplacementTransactionId()
        {
            int start = _nextRoadReplacementTransactionId;
            while (_nextRoadReplacementTransactionId <= 0 ||
                   RoadReplacementTransactions.ContainsKey(_nextRoadReplacementTransactionId))
            {
                _nextRoadReplacementTransactionId++;
                if (_nextRoadReplacementTransactionId == start)
                    throw new InvalidOperationException("No PCT road-replacement transaction IDs are available.");
            }

            return _nextRoadReplacementTransactionId++;
        }
    }
}
