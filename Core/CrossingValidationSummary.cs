namespace PedestrianCrossingToolkit
{
    public struct CrossingValidationSummary
    {
        public static readonly CrossingValidationSummary Empty = new CrossingValidationSummary(
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            CrossingApplicationSummary.Empty,
            CrossingNetworkValidationSummary.Empty,
            CrossingConnectivitySummary.Empty,
            CrossingPathWorkOrderSummary.Empty,
            CrossingLandingConnectorSummary.Empty,
            CrossingConstructionSummary.Empty,
            CrossingPathExecutionSummary.Empty,
            CrossingPathBuilder.BuiltConnectorValidationSummary.Empty);

        public readonly int Assets;
        public readonly int ValidPlans;
        public readonly int InvalidPlans;
        public readonly int StalePlans;
        public readonly int MissingNetworkReferences;
        public readonly int BlockedNetworkOperations;
        public readonly int MissingPrefabWorkItems;
        public readonly int UnresolvedLandings;
        public readonly int AssetsWithoutLinks;
        public readonly int MissingBuiltStructures;
        public readonly int MissingBuiltSegments;
        public readonly int UnattachedSurfaceTerminalNodes;
        public readonly CrossingApplicationSummary Application;
        public readonly CrossingNetworkValidationSummary NetworkValidation;
        public readonly CrossingConnectivitySummary Connectivity;
        public readonly CrossingPathWorkOrderSummary PathWorkOrders;
        public readonly CrossingLandingConnectorSummary LandingConnectors;
        public readonly CrossingConstructionSummary Construction;
        public readonly CrossingPathExecutionSummary PathExecution;
        public readonly CrossingPathBuilder.BuiltConnectorValidationSummary BuiltConnectors;

        public CrossingValidationSummary(
            int assets,
            int validPlans,
            int invalidPlans,
            int stalePlans,
            int missingNetworkReferences,
            int blockedNetworkOperations,
            int missingPrefabWorkItems,
            int unresolvedLandings,
            int assetsWithoutLinks,
            int missingBuiltStructures,
            int missingBuiltSegments,
            int unattachedSurfaceTerminalNodes,
            CrossingApplicationSummary application,
            CrossingNetworkValidationSummary networkValidation,
            CrossingConnectivitySummary connectivity,
            CrossingPathWorkOrderSummary pathWorkOrders,
            CrossingLandingConnectorSummary landingConnectors,
            CrossingConstructionSummary construction,
            CrossingPathExecutionSummary pathExecution,
            CrossingPathBuilder.BuiltConnectorValidationSummary builtConnectors)
        {
            Assets = assets;
            ValidPlans = validPlans;
            InvalidPlans = invalidPlans;
            StalePlans = stalePlans;
            MissingNetworkReferences = missingNetworkReferences;
            BlockedNetworkOperations = blockedNetworkOperations;
            MissingPrefabWorkItems = missingPrefabWorkItems;
            UnresolvedLandings = unresolvedLandings;
            AssetsWithoutLinks = assetsWithoutLinks;
            MissingBuiltStructures = missingBuiltStructures;
            MissingBuiltSegments = missingBuiltSegments;
            UnattachedSurfaceTerminalNodes = unattachedSurfaceTerminalNodes;
            Application = application;
            NetworkValidation = networkValidation;
            Connectivity = connectivity;
            PathWorkOrders = pathWorkOrders;
            LandingConnectors = landingConnectors;
            Construction = construction;
            PathExecution = pathExecution;
            BuiltConnectors = builtConnectors;
        }

        public int IssueCount
        {
            get
            {
                return InvalidPlans
                       + StalePlans
                       + MissingNetworkReferences
                       + BlockedNetworkOperations
                       + MissingPrefabWorkItems
                       + UnresolvedLandings
                       + MissingBuiltStructures
                       + MissingBuiltSegments;
            }
        }

        public int DiagnosticNoteCount
        {
            get { return AssetsWithoutLinks + UnattachedSurfaceTerminalNodes; }
        }

        public bool HasIssues
        {
            get { return IssueCount > 0; }
        }

        public string ToStatusString()
        {
            if (Assets <= 0)
                return "No crossings to validate.";

            if (!HasIssues)
            {
                return DiagnosticNoteCount > 0
                    ? "Validated " + Assets + " crossing" + Plural(Assets) + ": no player action needed. Diagnostic notes logged."
                    : "Validated " + Assets + " crossing" + Plural(Assets) + ": no player action needed.";
            }

            return "Validated " + Assets + " crossing" + Plural(Assets) + ": "
                   + IssueCount + " actionable issue" + Plural(IssueCount) + ". "
                   + ToShortIssueString();
        }

        public string ToLogString()
        {
            return "assets=" + Assets
                   + " validPlans=" + ValidPlans
                   + " invalidPlans=" + InvalidPlans
                   + " stalePlans=" + StalePlans
                   + " missingNetworkRefs=" + MissingNetworkReferences
                   + " blockedNetworkOps=" + BlockedNetworkOperations
                   + " missingPrefabWorkItems=" + MissingPrefabWorkItems
                   + " unresolvedLandings=" + UnresolvedLandings
                   + " assetsWithoutLinks=" + AssetsWithoutLinks
                   + " missingBuiltStructures=" + MissingBuiltStructures
                   + " missingBuiltSegments=" + MissingBuiltSegments
                   + " unattachedSurfaceTerminalNodes=" + UnattachedSurfaceTerminalNodes
                   + " application={" + Application.ToLogString() + "}"
                   + " networkValidation={" + NetworkValidation.ToLogString() + "}"
                   + " connectivity={" + Connectivity.ToLogString() + "}"
                   + " pathWorkOrders={" + PathWorkOrders.ToLogString() + "}"
                   + " landingConnectors={" + LandingConnectors.ToLogString() + "}"
                   + " construction={" + Construction.ToLogString() + "}"
                   + " pathExecution={" + PathExecution.ToLogString() + "}"
                   + " builtConnectors={" + BuiltConnectors.ToLogString() + "}";
        }

        public string ToShortIssueStringForUserInfo()
        {
            string text = ToShortIssueString();
            return string.IsNullOrEmpty(text) ? "none" : text;
        }

        private string ToShortIssueString()
        {
            string text = string.Empty;
            AppendIssue(ref text, "invalid", InvalidPlans);
            AppendIssue(ref text, "stale", StalePlans);
            AppendIssue(ref text, "missing network", MissingNetworkReferences);
            AppendIssue(ref text, "blocked ops", BlockedNetworkOperations);
            AppendIssue(ref text, "missing prefabs", MissingPrefabWorkItems);
            AppendIssue(ref text, "unresolved landings", UnresolvedLandings);
            AppendIssue(ref text, "missing built", MissingBuiltStructures + MissingBuiltSegments);
            return text;
        }

        private static void AppendIssue(ref string text, string label, int count)
        {
            if (count <= 0)
                return;

            if (text.Length > 0)
                text += " ";

            text += label + "=" + count + ".";
        }

        private static string Plural(int count)
        {
            return count == 1 ? string.Empty : "s";
        }
    }
}
