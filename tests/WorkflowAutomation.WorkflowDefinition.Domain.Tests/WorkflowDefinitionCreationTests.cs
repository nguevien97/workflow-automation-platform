using WorkflowAutomation.SharedKernel.Domain.Enums;
using WorkflowAutomation.SharedKernel.Domain.Ids;
using WorkflowAutomation.WorkflowDefinition.Domain.Aggregates;
using WorkflowAutomation.WorkflowDefinition.Domain.Enums;
using WorkflowAutomation.WorkflowDefinition.Domain.Ids;
using WorkflowAutomation.WorkflowDefinition.Domain.StepDefinitions;
using WorkflowAutomation.WorkflowDefinition.Domain.ValueObjects;
// using WorkflowDefinition = WorkflowAutomation.WorkflowDefinition.Domain.Aggregates.WorkflowDefinition;


namespace WorkflowAutomation.WorkflowDefinition.Tests;

public class WorkflowDefinitionTests
{
    #region Helpers

    private static StepId Id() => StepId.New();
    private static WorkflowVersionId VersionId() => WorkflowVersionId.New();
    private static WorkflowId WfId() => WorkflowId.New();
    private static IntegrationId IntId() => IntegrationId.New();

    private static StepOutputSchema Schema(params (string name, string type)[] fields)
        => new(fields.ToDictionary(f => f.name, f => f.type));

    private static TriggerStepDefinition Trigger(string name, StepId id, StepId nextStepId, StepOutputSchema outputSchema)
        => new(id, name, IntId(), "onEvent", new Dictionary<string, string>(), nextStepId: nextStepId, outputSchema: outputSchema);

    private static ActionStepDefinition Action(string name, StepId id, StepOutputSchema outputSchema,
        Dictionary<string, TemplateOrLiteral>? inputMappings = null, StepId? nextStepId = null,
        FailureStrategy failureStrategy = FailureStrategy.Stop, int retryCount = 0)
        => new(id, name, IntId(), "execute", inputMappings ?? new(), failureStrategy, retryCount, outputSchema: outputSchema, nextStepId: nextStepId);

    private static ConditionStepDefinition Condition(string name, StepId id, IReadOnlyList<ConditionRule> rules,
        StepId? nextStepId = null, StepId? fallbackStepId = null)
        => new(id, name, rules, nextStepId, fallbackStepId);

    private static ParallelStepDefinition Parallel(string name, StepId id, IReadOnlyList<StepId> branchEntryStepIds,
        StepId? nextStepId = null)
        => new(id, name, branchEntryStepIds, nextStepId);

    private static LoopStepDefinition Loop(string name, StepId id, TemplateReference sourceArray, StepId loopEntryStepId,
        StepOutputSchema outputSchema, StepId? nextStepId = null)
        => new(id, name, sourceArray, loopEntryStepId,
            triggerOutputSchema: Schema(("item", "object")),
            outputSchema: outputSchema,
            concurrencyMode: ConcurrencyMode.Sequential,
            iterationFailureStrategy: IterationFailureStrategy.Skip,
            nextStepId: nextStepId);

    private static ConditionRule Rule(string expression, StepId targetStepId) => new(expression, targetStepId);

    private static Dictionary<string, TemplateOrLiteral> Input(params (string key, string value, bool isTemplate)[] entries)
        => entries.ToDictionary(e => e.key, e => e.isTemplate ? TemplateOrLiteral.Template(e.value) : TemplateOrLiteral.Literal(e.value));

    private WorkflowAutomation.WorkflowDefinition.Domain.Aggregates.WorkflowDefinition Build(List<StepDefinition> steps) => new(VersionId(), WfId(), steps);

    #endregion

    // =========================================================================
    // VALID WORKFLOWS — Complex Real-World Scenarios
    // =========================================================================

    #region 1. E-Commerce Order Processing Pipeline (28 steps)

    /// <summary>
    /// Trigger(NewOrder) → ValidateOrder → CheckInventory → Condition(InStock?)
    ///   Yes → Parallel(
    ///     Branch1: ReserveStock → GenerateInvoice → ProcessPayment → ConfirmPayment,
    ///     Branch2: CalcShipping → SelectCarrier → GenerateLabel,
    ///     Branch3: CheckFraud → ScoreFraud
    ///   ) → MergeOrderData → Loop(each item: PackItem → WeighItem → LabelItem) → SchedulePickup → SendConfirmation → UpdateCRM
    ///   No → NotifyCustomer → CancelOrder → RefundIfPaid
    /// → WriteAuditLog
    /// </summary>
    [Fact]
    public void Valid_01_ECommerceOrderProcessing_28Steps()
    {
        var trigger = Id();
        var validate = Id(); var checkInv = Id(); var cond = Id();
        // Yes branch → Parallel
        var par = Id();
        var reserveStock = Id(); var genInvoice = Id(); var processPayment = Id(); var confirmPayment = Id();
        var calcShipping = Id(); var selectCarrier = Id(); var genLabel = Id();
        var checkFraud = Id(); var scoreFraud = Id();
        var mergeOrder = Id();
        var loop = Id(); var packItem = Id(); var weighItem = Id(); var labelItem = Id();
        var schedulePickup = Id(); var sendConfirm = Id(); var updateCrm = Id();
        // No branch
        var notifyCust = Id(); var cancelOrder = Id(); var refund = Id();
        // Merge
        var auditLog = Id();

        var steps = new List<StepDefinition>
        {
            Trigger("NewOrder", trigger, validate, Schema(("orderId", "string"), ("customerId", "string"), ("items", "array"), ("totalAmount", "decimal"))),
            Action("ValidateOrder", validate, Schema(("isValid", "string"), ("errors", "string")),
                Input(("orderId", "{{NewOrder.orderId}}", true)), nextStepId: checkInv),
            Action("CheckInventory", checkInv, Schema(("allInStock", "string"), ("unavailableItems", "string")),
                Input(("items", "{{NewOrder.items}}", true)), nextStepId: cond),
            Condition("InStockCheck", cond,
                rules: [Rule("{{CheckInventory.allInStock}} == 'true'", par)],
                fallbackStepId: notifyCust, nextStepId: auditLog),

            // ── Yes branch: Parallel fulfillment ──
            Parallel("FulfillOrder", par, [reserveStock, calcShipping, checkFraud], nextStepId: mergeOrder),

            Action("ReserveStock", reserveStock, Schema(("reservationId", "string")),
                Input(("items", "{{NewOrder.items}}", true)), nextStepId: genInvoice),
            Action("GenerateInvoice", genInvoice, Schema(("invoiceId", "string"), ("invoiceUrl", "string")),
                Input(("orderId", "{{NewOrder.orderId}}", true), ("amount", "{{NewOrder.totalAmount}}", true)), nextStepId: processPayment),
            Action("ProcessPayment", processPayment, Schema(("transactionId", "string"), ("status", "string")),
                Input(("invoiceId", "{{GenerateInvoice.invoiceId}}", true), ("amount", "{{NewOrder.totalAmount}}", true)),
                nextStepId: confirmPayment, failureStrategy: FailureStrategy.Retry, retryCount: 3),
            Action("ConfirmPayment", confirmPayment, Schema(("receiptUrl", "string")),
                Input(("transactionId", "{{ProcessPayment.transactionId}}", true))),

            Action("CalcShipping", calcShipping, Schema(("shippingCost", "decimal"), ("estimatedDays", "string")),
                Input(("items", "{{NewOrder.items}}", true)), nextStepId: selectCarrier),
            Action("SelectCarrier", selectCarrier, Schema(("carrierId", "string"), ("carrierName", "string")),
                Input(("cost", "{{CalcShipping.shippingCost}}", true)), nextStepId: genLabel),
            Action("GenerateLabel", genLabel, Schema(("labelUrl", "string"), ("trackingNumber", "string")),
                Input(("carrierId", "{{SelectCarrier.carrierId}}", true))),

            Action("CheckFraud", checkFraud, Schema(("riskIndicators", "string")),
                Input(("customerId", "{{NewOrder.customerId}}", true), ("amount", "{{NewOrder.totalAmount}}", true)), nextStepId: scoreFraud),
            Action("ScoreFraud", scoreFraud, Schema(("fraudScore", "decimal"), ("recommendation", "string")),
                Input(("indicators", "{{CheckFraud.riskIndicators}}", true))),

            // Merge after parallel — references outputs from all 3 branches
            Action("MergeOrderData", mergeOrder, Schema(("consolidatedOrder", "string")),
                Input(("receipt", "{{ConfirmPayment.receiptUrl}}", true),
                      ("tracking", "{{GenerateLabel.trackingNumber}}", true),
                      ("fraudScore", "{{ScoreFraud.fraudScore}}", true)),
                nextStepId: loop),

            // Loop over each item to pack
            Loop("PackEachItem", loop, new TemplateReference("{{NewOrder.items}}"), packItem,
                Schema(("packedItems", "array")), nextStepId: schedulePickup),
            Action("PackItem", packItem, Schema(("packageId", "string")),
                Input(("reservationId", "{{ReserveStock.reservationId}}", true)), nextStepId: weighItem),
            Action("WeighItem", weighItem, Schema(("weight", "decimal")),
                Input(("packageId", "{{PackItem.packageId}}", true)), nextStepId: labelItem),
            Action("LabelItem", labelItem, Schema(("labelledPackageId", "string")),
                Input(("packageId", "{{PackItem.packageId}}", true), ("weight", "{{WeighItem.weight}}", true))),

            Action("SchedulePickup", schedulePickup, Schema(("pickupId", "string"), ("pickupTime", "string")),
                Input(("carrierId", "{{SelectCarrier.carrierId}}", true), ("packages", "{{PackEachItem.packedItems}}", true)),
                nextStepId: sendConfirm),
            Action("SendConfirmation", sendConfirm, Schema(("emailId", "string")),
                Input(("orderId", "{{NewOrder.orderId}}", true), ("tracking", "{{GenerateLabel.trackingNumber}}", true),
                      ("pickupTime", "{{SchedulePickup.pickupTime}}", true)),
                nextStepId: updateCrm),
            Action("UpdateCRM", updateCrm, Schema(("crmRecordId", "string")),
                Input(("customerId", "{{NewOrder.customerId}}", true), ("orderId", "{{NewOrder.orderId}}", true))),

            // ── No branch ──
            Action("NotifyCustomer", notifyCust, Schema(("notificationId", "string")),
                Input(("customerId", "{{NewOrder.customerId}}", true), ("unavailable", "{{CheckInventory.unavailableItems}}", true)),
                nextStepId: cancelOrder),
            Action("CancelOrder", cancelOrder, Schema(("cancellationId", "string")),
                Input(("orderId", "{{NewOrder.orderId}}", true)), nextStepId: refund),
            Action("RefundIfPaid", refund, Schema(("refundId", "string")),
                Input(("orderId", "{{NewOrder.orderId}}", true))),

            // ── Merge point ──
            Action("WriteAuditLog", auditLog, Schema(("auditId", "string"), ("logTimestamp", "string")),
                Input(("orderId", "{{NewOrder.orderId}}", true), ("inventory", "{{CheckInventory.allInStock}}", true))),
        };

        Assert.Equal(26, steps.Count);
        var ex = Record.Exception(() => Build(steps));
        Assert.Null(ex);
    }

    #endregion

    #region 2. CI/CD Deployment Pipeline (25 steps)

    /// <summary>
    /// Trigger(PushEvent) → FetchCode → Parallel(
    ///   Branch1: UnitTests → CoverageReport,
    ///   Branch2: LintCode → SecurityScan,
    ///   Branch3: BuildDocker → PushImage
    /// ) → MergeResults → Condition(AllPassed?)
    ///   Yes → DeployStaging → RunE2E → Condition(E2EPassed?)
    ///     Yes → DeployProd → HealthCheck → NotifyTeam,
    ///     No  → RollbackStaging → AlertOncall
    ///   No  → NotifyAuthor → CreateJiraTicket
    /// → UpdateDashboard
    /// </summary>
    [Fact]
    public void Valid_02_CICDDeploymentPipeline_25Steps()
    {
        var trigger = Id();
        var fetchCode = Id(); var par = Id();
        var unitTests = Id(); var coverage = Id();
        var lint = Id(); var secScan = Id();
        var buildDocker = Id(); var pushImage = Id();
        var mergeResults = Id(); var cond1 = Id();
        var deployStaging = Id(); var runE2e = Id(); var cond2 = Id();
        var deployProd = Id(); var healthCheck = Id(); var notifyTeam = Id();
        var rollback = Id(); var alertOncall = Id();
        var notifyAuthor = Id(); var createJira = Id();
        var updateDash = Id();

        var steps = new List<StepDefinition>
        {
            Trigger("PushEvent", trigger, fetchCode,
                Schema(("repoUrl", "string"), ("branch", "string"), ("commitSha", "string"), ("authorEmail", "string"))),
            Action("FetchCode", fetchCode, Schema(("codePath", "string"), ("commitMessage", "string")),
                Input(("repo", "{{PushEvent.repoUrl}}", true), ("sha", "{{PushEvent.commitSha}}", true)), nextStepId: par),

            Parallel("QualityGates", par, [unitTests, lint, buildDocker], nextStepId: mergeResults),

            Action("RunUnitTests", unitTests, Schema(("testsPassed", "string"), ("testCount", "string")),
                Input(("code", "{{FetchCode.codePath}}", true)), nextStepId: coverage),
            Action("GenerateCoverage", coverage, Schema(("coveragePercent", "decimal"), ("reportUrl", "string")),
                Input(("testResults", "{{RunUnitTests.testsPassed}}", true))),

            Action("LintCode", lint, Schema(("lintErrors", "string"), ("warnings", "string")),
                Input(("code", "{{FetchCode.codePath}}", true)), nextStepId: secScan),
            Action("SecurityScan", secScan, Schema(("vulnerabilities", "string"), ("severity", "string")),
                Input(("code", "{{FetchCode.codePath}}", true))),

            Action("BuildDockerImage", buildDocker, Schema(("imageTag", "string"), ("imageSizeBytes", "string")),
                Input(("code", "{{FetchCode.codePath}}", true), ("sha", "{{PushEvent.commitSha}}", true)), nextStepId: pushImage),
            Action("PushToRegistry", pushImage, Schema(("registryUrl", "string")),
                Input(("imageTag", "{{BuildDockerImage.imageTag}}", true))),

            Action("MergeQualityResults", mergeResults, Schema(("allPassed", "string"), ("summary", "string")),
                Input(("tests", "{{RunUnitTests.testsPassed}}", true), ("coverage", "{{GenerateCoverage.coveragePercent}}", true),
                      ("lint", "{{LintCode.lintErrors}}", true), ("vulns", "{{SecurityScan.vulnerabilities}}", true)),
                nextStepId: cond1),

            Condition("AllQualityPassed", cond1,
                rules: [Rule("{{MergeQualityResults.allPassed}} == 'true'", deployStaging)],
                fallbackStepId: notifyAuthor, nextStepId: updateDash),

            // Yes: deploy staging → e2e → condition
            Action("DeployStaging", deployStaging, Schema(("stagingUrl", "string"), ("deployId", "string")),
                Input(("image", "{{PushToRegistry.registryUrl}}", true)), nextStepId: runE2e),
            Action("RunE2ETests", runE2e, Schema(("e2ePassed", "string"), ("failedTests", "string")),
                Input(("url", "{{DeployStaging.stagingUrl}}", true)), nextStepId: cond2),

            Condition("E2EPassed", cond2,
                rules: [Rule("{{RunE2ETests.e2ePassed}} == 'true'", deployProd)],
                fallbackStepId: rollback),

            Action("DeployProduction", deployProd, Schema(("prodUrl", "string")),
                Input(("image", "{{PushToRegistry.registryUrl}}", true)), nextStepId: healthCheck),
            Action("HealthCheck", healthCheck, Schema(("healthy", "string"), ("latencyMs", "string")),
                Input(("url", "{{DeployProduction.prodUrl}}", true)), nextStepId: notifyTeam),
            Action("NotifyTeam", notifyTeam, Schema(("slackMessageId", "string")),
                Input(("message", "deployed", false), ("url", "{{DeployProduction.prodUrl}}", true))),

            Action("RollbackStaging", rollback, Schema(("rollbackId", "string")),
                Input(("deployId", "{{DeployStaging.deployId}}", true)), nextStepId: alertOncall),
            Action("AlertOncall", alertOncall, Schema(("alertId", "string")),
                Input(("failures", "{{RunE2ETests.failedTests}}", true))),

            // No: quality failed
            Action("NotifyAuthor", notifyAuthor, Schema(("emailId", "string")),
                Input(("email", "{{PushEvent.authorEmail}}", true), ("summary", "{{MergeQualityResults.summary}}", true)),
                nextStepId: createJira),
            Action("CreateJiraTicket", createJira, Schema(("ticketId", "string")),
                Input(("summary", "{{MergeQualityResults.summary}}", true), ("sha", "{{PushEvent.commitSha}}", true))),

            // Final merge
            Action("UpdateDashboard", updateDash, Schema(("dashboardUrl", "string")),
                Input(("sha", "{{PushEvent.commitSha}}", true), ("branch", "{{PushEvent.branch}}", true))),
        };

        Assert.Equal(22, steps.Count);
        var ex = Record.Exception(() => Build(steps));
        Assert.Null(ex);
    }

    #endregion

    #region 3. Customer Onboarding Pipeline (22 steps)

    /// <summary>
    /// Trigger(SignUp) → ValidateEmail → CreateAccount → Parallel(
    ///   Branch1: SendWelcomeEmail → ScheduleFollowUp,
    ///   Branch2: CreateCRMRecord → AssignSalesRep → ScheduleDemo,
    ///   Branch3: ProvisionTenant → SeedDefaultData → ConfigureIntegrations
    /// ) → MergeOnboarding → Condition(IsPaidPlan?)
    ///   Yes → GenerateInvoice → ChargeCard → SendReceipt,
    ///   No  → ScheduleTrialReminder
    /// → ActivateAccount → NotifyInternalTeam
    /// </summary>
    [Fact]
    public void Valid_03_CustomerOnboardingPipeline_22Steps()
    {
        var trigger = Id();
        var validateEmail = Id(); var createAcct = Id(); var par = Id();
        var sendWelcome = Id(); var scheduleFollow = Id();
        var createCrm = Id(); var assignRep = Id(); var scheduleDemo = Id();
        var provTenant = Id(); var seedData = Id(); var configInteg = Id();
        var mergeOnboard = Id(); var cond = Id();
        var genInvoice = Id(); var chargeCard = Id(); var sendReceipt = Id();
        var schedTrial = Id();
        var activateAcct = Id(); var notifyInternal = Id();

        var steps = new List<StepDefinition>
        {
            Trigger("UserSignUp", trigger, validateEmail,
                Schema(("email", "string"), ("name", "string"), ("plan", "string"), ("paymentMethod", "string"))),
            Action("ValidateEmail", validateEmail, Schema(("isValid", "string"), ("domain", "string")),
                Input(("email", "{{UserSignUp.email}}", true)), nextStepId: createAcct),
            Action("CreateAccount", createAcct, Schema(("accountId", "string"), ("apiKey", "string")),
                Input(("email", "{{UserSignUp.email}}", true), ("name", "{{UserSignUp.name}}", true)), nextStepId: par),

            Parallel("OnboardingTasks", par, [sendWelcome, createCrm, provTenant], nextStepId: mergeOnboard),

            Action("SendWelcomeEmail", sendWelcome, Schema(("emailId", "string")),
                Input(("email", "{{UserSignUp.email}}", true), ("name", "{{UserSignUp.name}}", true)), nextStepId: scheduleFollow),
            Action("ScheduleFollowUp", scheduleFollow, Schema(("taskId", "string")),
                Input(("accountId", "{{CreateAccount.accountId}}", true))),

            Action("CreateCRMRecord", createCrm, Schema(("crmId", "string")),
                Input(("name", "{{UserSignUp.name}}", true), ("email", "{{UserSignUp.email}}", true)), nextStepId: assignRep),
            Action("AssignSalesRep", assignRep, Schema(("repId", "string"), ("repName", "string")),
                Input(("crmId", "{{CreateCRMRecord.crmId}}", true)), nextStepId: scheduleDemo),
            Action("ScheduleDemo", scheduleDemo, Schema(("meetingUrl", "string")),
                Input(("repId", "{{AssignSalesRep.repId}}", true), ("email", "{{UserSignUp.email}}", true))),

            Action("ProvisionTenant", provTenant, Schema(("tenantId", "string"), ("tenantUrl", "string")),
                Input(("accountId", "{{CreateAccount.accountId}}", true)), nextStepId: seedData),
            Action("SeedDefaultData", seedData, Schema(("seeded", "string")),
                Input(("tenantId", "{{ProvisionTenant.tenantId}}", true)), nextStepId: configInteg),
            Action("ConfigureIntegrations", configInteg, Schema(("integrationsEnabled", "string")),
                Input(("tenantId", "{{ProvisionTenant.tenantId}}", true), ("apiKey", "{{CreateAccount.apiKey}}", true))),

            Action("MergeOnboarding", mergeOnboard, Schema(("onboardingComplete", "string")),
                Input(("tenant", "{{ProvisionTenant.tenantUrl}}", true), ("rep", "{{AssignSalesRep.repName}}", true)),
                nextStepId: cond),

            Condition("IsPaidPlan", cond,
                rules: [Rule("{{UserSignUp.plan}} == 'paid'", genInvoice)],
                fallbackStepId: schedTrial, nextStepId: activateAcct),

            Action("GenerateInvoice", genInvoice, Schema(("invoiceId", "string")),
                Input(("accountId", "{{CreateAccount.accountId}}", true)), nextStepId: chargeCard),
            Action("ChargeCard", chargeCard, Schema(("chargeId", "string"), ("chargeStatus", "string")),
                Input(("paymentMethod", "{{UserSignUp.paymentMethod}}", true)), nextStepId: sendReceipt,
                failureStrategy: FailureStrategy.Retry, retryCount: 2),
            Action("SendReceipt", sendReceipt, Schema(("receiptUrl", "string")),
                Input(("email", "{{UserSignUp.email}}", true), ("chargeId", "{{ChargeCard.chargeId}}", true))),

            Action("ScheduleTrialReminder", schedTrial, Schema(("reminderId", "string")),
                Input(("accountId", "{{CreateAccount.accountId}}", true))),

            Action("ActivateAccount", activateAcct, Schema(("activatedAt", "string")),
                Input(("accountId", "{{CreateAccount.accountId}}", true)), nextStepId: notifyInternal),
            Action("NotifyInternalTeam", notifyInternal, Schema(("slackId", "string")),
                Input(("name", "{{UserSignUp.name}}", true), ("plan", "{{UserSignUp.plan}}", true))),
        };

        Assert.Equal(20, steps.Count);
        var ex = Record.Exception(() => Build(steps));
        Assert.Null(ex);
    }

    #endregion

    #region 4. Data ETL Pipeline — Loop-heavy with parallel pre-processing (24 steps)

    /// <summary>
    /// Trigger(ScheduledRun) → ConnectSource → FetchSchema → Parallel(
    ///   Branch1: ExtractTableA → ValidateA,
    ///   Branch2: ExtractTableB → ValidateB
    /// ) → MergeExtracts → Loop(each record: Transform → Enrich → Validate → LoadToWarehouse)
    /// → ReconcileCounts → Condition(Discrepancy?)
    ///   Yes → AlertDataTeam → CreateIncident → PauseDownstream,
    ///   No  → MarkSuccess
    /// → UpdateCatalog → NotifyStakeholders
    /// </summary>
    [Fact]
    public void Valid_04_DataETLPipeline_24Steps()
    {
        var trigger = Id();
        var connectSrc = Id(); var fetchSchema = Id(); var par = Id();
        var extractA = Id(); var validateA = Id();
        var extractB = Id(); var validateB = Id();
        var mergeExtracts = Id(); var loop = Id();
        var transform = Id(); var enrich = Id(); var validate = Id(); var loadToWh = Id();
        var reconcile = Id(); var cond = Id();
        var alertTeam = Id(); var createIncident = Id(); var pauseDown = Id();
        var markSuccess = Id();
        var updateCatalog = Id(); var notifyStake = Id();

        var steps = new List<StepDefinition>
        {
            Trigger("ScheduledETLRun", trigger, connectSrc,
                Schema(("runId", "string"), ("sourceConfig", "string"), ("targetSchema", "string"))),
            Action("ConnectToSource", connectSrc, Schema(("connectionId", "string")),
                Input(("config", "{{ScheduledETLRun.sourceConfig}}", true)), nextStepId: fetchSchema),
            Action("FetchSourceSchema", fetchSchema, Schema(("tables", "array"), ("rowEstimate", "string")),
                Input(("connectionId", "{{ConnectToSource.connectionId}}", true)), nextStepId: par),

            Parallel("ExtractTables", par, [extractA, extractB], nextStepId: mergeExtracts),

            Action("ExtractTableA", extractA, Schema(("recordsA", "array"), ("countA", "string")),
                Input(("connectionId", "{{ConnectToSource.connectionId}}", true)), nextStepId: validateA),
            Action("ValidateSchemaA", validateA, Schema(("validA", "string")),
                Input(("records", "{{ExtractTableA.recordsA}}", true))),

            Action("ExtractTableB", extractB, Schema(("recordsB", "array"), ("countB", "string")),
                Input(("connectionId", "{{ConnectToSource.connectionId}}", true)), nextStepId: validateB),
            Action("ValidateSchemaB", validateB, Schema(("validB", "string")),
                Input(("records", "{{ExtractTableB.recordsB}}", true))),

            Action("MergeExtracts", mergeExtracts, Schema(("allRecords", "array"), ("totalCount", "string")),
                Input(("a", "{{ExtractTableA.recordsA}}", true), ("b", "{{ExtractTableB.recordsB}}", true)),
                nextStepId: loop),

            Loop("TransformEachRecord", loop, new TemplateReference("{{MergeExtracts.allRecords}}"), transform,
                Schema(("loadedRecords", "array")), nextStepId: reconcile),

            Action("TransformRecord", transform, Schema(("transformed", "string")),
                Input(("targetSchema", "{{ScheduledETLRun.targetSchema}}", true)), nextStepId: enrich),
            Action("EnrichRecord", enrich, Schema(("enriched", "string")),
                Input(("data", "{{TransformRecord.transformed}}", true)), nextStepId: validate),
            Action("ValidateRecord", validate, Schema(("isValid", "string"), ("errors", "string")),
                Input(("record", "{{EnrichRecord.enriched}}", true)), nextStepId: loadToWh),
            Action("LoadToWarehouse", loadToWh, Schema(("warehouseId", "string")),
                Input(("record", "{{EnrichRecord.enriched}}", true), ("valid", "{{ValidateRecord.isValid}}", true))),

            Action("ReconcileCounts", reconcile, Schema(("expectedCount", "string"), ("actualCount", "string"), ("discrepancy", "string")),
                Input(("expected", "{{MergeExtracts.totalCount}}", true), ("loaded", "{{TransformEachRecord.loadedRecords}}", true)),
                nextStepId: cond),

            Condition("HasDiscrepancy", cond,
                rules: [Rule("{{ReconcileCounts.discrepancy}} != '0'", alertTeam)],
                fallbackStepId: markSuccess, nextStepId: updateCatalog),

            Action("AlertDataTeam", alertTeam, Schema(("alertId", "string")),
                Input(("discrepancy", "{{ReconcileCounts.discrepancy}}", true)), nextStepId: createIncident),
            Action("CreateIncident", createIncident, Schema(("incidentId", "string")),
                Input(("alertId", "{{AlertDataTeam.alertId}}", true)), nextStepId: pauseDown),
            Action("PauseDownstream", pauseDown, Schema(("paused", "string")),
                Input(("incidentId", "{{CreateIncident.incidentId}}", true))),

            Action("MarkSuccess", markSuccess, Schema(("successTimestamp", "string")),
                Input(("runId", "{{ScheduledETLRun.runId}}", true))),

            Action("UpdateDataCatalog", updateCatalog, Schema(("catalogEntry", "string")),
                Input(("runId", "{{ScheduledETLRun.runId}}", true)), nextStepId: notifyStake),
            Action("NotifyStakeholders", notifyStake, Schema(("emailId", "string")),
                Input(("runId", "{{ScheduledETLRun.runId}}", true))),
        };

        Assert.Equal(22, steps.Count);
        var ex = Record.Exception(() => Build(steps));
        Assert.Null(ex);
    }

    #endregion

    #region 5. Incident Management Pipeline (23 steps)

    /// <summary>
    /// Trigger(Alert) → EnrichAlert → ClassifySeverity → Condition(Severity?)
    ///   Critical → Parallel(
    ///     Branch1: PageOncall → AckWait → EscalateIfNoAck,
    ///     Branch2: CreateWarRoom → InviteStakeholders,
    ///     Branch3: PauseDeployments
    ///   ) → MergeCritical → RunDiagnostics → ApplyFix → VerifyFix,
    ///   Warning → CreateTicket → AssignEngineer → ScheduleReview,
    ///   Info → LogAndDismiss
    /// → UpdateStatusPage → PostMortemTemplate
    /// </summary>
    [Fact]
    public void Valid_05_IncidentManagementPipeline_23Steps()
    {
        var trigger = Id();
        var enrichAlert = Id(); var classifySev = Id(); var cond = Id();
        var par = Id();
        var pageOncall = Id(); var ackWait = Id(); var escalate = Id();
        var createWarRoom = Id(); var inviteStake = Id();
        var pauseDeploys = Id();
        var mergeCritical = Id(); var runDiag = Id(); var applyFix = Id(); var verifyFix = Id();
        var createTicket = Id(); var assignEng = Id(); var schedReview = Id();
        var logDismiss = Id();
        var updateStatus = Id(); var postMortem = Id();

        var steps = new List<StepDefinition>
        {
            Trigger("AlertFired", trigger, enrichAlert,
                Schema(("alertId", "string"), ("source", "string"), ("message", "string"), ("metrics", "string"))),
            Action("EnrichAlert", enrichAlert, Schema(("service", "string"), ("region", "string"), ("impactedUsers", "string")),
                Input(("alertId", "{{AlertFired.alertId}}", true)), nextStepId: classifySev),
            Action("ClassifySeverity", classifySev, Schema(("severity", "string"), ("category", "string")),
                Input(("message", "{{AlertFired.message}}", true), ("users", "{{EnrichAlert.impactedUsers}}", true)),
                nextStepId: cond),

            Condition("SeverityRouter", cond,
                rules: [
                    Rule("{{ClassifySeverity.severity}} == 'critical'", par),
                    Rule("{{ClassifySeverity.severity}} == 'warning'", createTicket)
                ],
                fallbackStepId: logDismiss, nextStepId: updateStatus),

            // Critical branch: Parallel response
            Parallel("CriticalResponse", par, [pageOncall, createWarRoom, pauseDeploys], nextStepId: mergeCritical),

            Action("PageOncall", pageOncall, Schema(("pageId", "string")),
                Input(("service", "{{EnrichAlert.service}}", true)), nextStepId: ackWait),
            Action("WaitForAck", ackWait, Schema(("acknowledged", "string"), ("responder", "string")),
                Input(("pageId", "{{PageOncall.pageId}}", true)), nextStepId: escalate),
            Action("EscalateIfNoAck", escalate, Schema(("escalationId", "string")),
                Input(("acked", "{{WaitForAck.acknowledged}}", true))),

            Action("CreateWarRoom", createWarRoom, Schema(("channelId", "string"), ("channelUrl", "string")),
                Input(("service", "{{EnrichAlert.service}}", true)), nextStepId: inviteStake),
            Action("InviteStakeholders", inviteStake, Schema(("invitedCount", "string")),
                Input(("channel", "{{CreateWarRoom.channelId}}", true))),

            Action("PauseDeployments", pauseDeploys, Schema(("pausedServices", "string")),
                Input(("region", "{{EnrichAlert.region}}", true))),

            Action("MergeCriticalActions", mergeCritical, Schema(("responder", "string"), ("warRoom", "string")),
                Input(("responder", "{{WaitForAck.responder}}", true), ("channel", "{{CreateWarRoom.channelUrl}}", true)),
                nextStepId: runDiag),
            Action("RunDiagnostics", runDiag, Schema(("rootCause", "string"), ("logs", "string")),
                Input(("service", "{{EnrichAlert.service}}", true), ("metrics", "{{AlertFired.metrics}}", true)), nextStepId: applyFix),
            Action("ApplyFix", applyFix, Schema(("fixId", "string"), ("fixType", "string")),
                Input(("rootCause", "{{RunDiagnostics.rootCause}}", true)), nextStepId: verifyFix,
                failureStrategy: FailureStrategy.Retry, retryCount: 2),
            Action("VerifyFix", verifyFix, Schema(("verified", "string")),
                Input(("fixId", "{{ApplyFix.fixId}}", true))),

            // Warning branch
            Action("CreateTicket", createTicket, Schema(("ticketId", "string")),
                Input(("message", "{{AlertFired.message}}", true), ("category", "{{ClassifySeverity.category}}", true)),
                nextStepId: assignEng),
            Action("AssignEngineer", assignEng, Schema(("assigneeId", "string")),
                Input(("ticketId", "{{CreateTicket.ticketId}}", true)), nextStepId: schedReview),
            Action("ScheduleReview", schedReview, Schema(("reviewDate", "string")),
                Input(("ticketId", "{{CreateTicket.ticketId}}", true), ("assignee", "{{AssignEngineer.assigneeId}}", true))),

            // Info branch
            Action("LogAndDismiss", logDismiss, Schema(("logId", "string")),
                Input(("alertId", "{{AlertFired.alertId}}", true))),

            // Merge
            Action("UpdateStatusPage", updateStatus, Schema(("statusPageUrl", "string")),
                Input(("alertId", "{{AlertFired.alertId}}", true), ("severity", "{{ClassifySeverity.severity}}", true)),
                nextStepId: postMortem),
            Action("CreatePostMortemTemplate", postMortem, Schema(("templateId", "string")),
                Input(("alertId", "{{AlertFired.alertId}}", true))),
        };

        Assert.Equal(21, steps.Count);
        var ex = Record.Exception(() => Build(steps));
        Assert.Null(ex);
    }

    #endregion

    #region 6. Marketing Campaign Pipeline (20 steps)

    /// <summary>
    /// Trigger(CampaignLaunch) → FetchAudience → SegmentAudience →
    /// Loop(each segment: PersonalizeContent → A/BTest → SelectWinner → DeliverContent)
    /// → AggregateMetrics → Condition(PerformanceOK?)
    ///   Yes → ScaleUp → ExtendBudget → ReportSuccess,
    ///   No  → PauseCampaign → AlertManager → AnalyzeFailure
    /// → ArchiveCampaign → UpdateDashboard
    /// </summary>
    [Fact]
    public void Valid_06_MarketingCampaignPipeline_20Steps()
    {
        var trigger = Id();
        var fetchAud = Id(); var segment = Id(); var loop = Id();
        var personalize = Id(); var abTest = Id(); var selectWin = Id(); var deliver = Id();
        var aggMetrics = Id(); var cond = Id();
        var scaleUp = Id(); var extBudget = Id(); var reportSuccess = Id();
        var pause = Id(); var alertMgr = Id(); var analyzeFailure = Id();
        var archive = Id(); var updateDash = Id();

        var steps = new List<StepDefinition>
        {
            Trigger("CampaignLaunch", trigger, fetchAud,
                Schema(("campaignId", "string"), ("budget", "decimal"), ("targetCriteria", "string"))),
            Action("FetchAudience", fetchAud, Schema(("audienceList", "array"), ("totalSize", "string")),
                Input(("criteria", "{{CampaignLaunch.targetCriteria}}", true)), nextStepId: segment),
            Action("SegmentAudience", segment, Schema(("segments", "array")),
                Input(("audience", "{{FetchAudience.audienceList}}", true)), nextStepId: loop),

            Loop("ProcessSegments", loop, new TemplateReference("{{SegmentAudience.segments}}"), personalize,
                Schema(("segmentResults", "array")), nextStepId: aggMetrics),

            Action("PersonalizeContent", personalize, Schema(("contentVariants", "array")),
                Input(("campaignId", "{{CampaignLaunch.campaignId}}", true)), nextStepId: abTest),
            Action("ABTest", abTest, Schema(("variantAMetrics", "string"), ("variantBMetrics", "string")),
                Input(("variants", "{{PersonalizeContent.contentVariants}}", true)), nextStepId: selectWin),
            Action("SelectWinner", selectWin, Schema(("winningVariant", "string")),
                Input(("metricsA", "{{ABTest.variantAMetrics}}", true), ("metricsB", "{{ABTest.variantBMetrics}}", true)),
                nextStepId: deliver),
            Action("DeliverContent", deliver, Schema(("deliveryId", "string"), ("deliveredCount", "string")),
                Input(("content", "{{SelectWinner.winningVariant}}", true))),

            Action("AggregateMetrics", aggMetrics, Schema(("totalDelivered", "string"), ("conversionRate", "decimal"), ("isPerforming", "string")),
                Input(("results", "{{ProcessSegments.segmentResults}}", true)), nextStepId: cond),

            Condition("PerformanceCheck", cond,
                rules: [Rule("{{AggregateMetrics.isPerforming}} == 'true'", scaleUp)],
                fallbackStepId: pause, nextStepId: archive),

            Action("ScaleUp", scaleUp, Schema(("scaledInstances", "string")),
                Input(("campaignId", "{{CampaignLaunch.campaignId}}", true)), nextStepId: extBudget),
            Action("ExtendBudget", extBudget, Schema(("newBudget", "decimal")),
                Input(("current", "{{CampaignLaunch.budget}}", true)), nextStepId: reportSuccess),
            Action("ReportSuccess", reportSuccess, Schema(("reportUrl", "string")),
                Input(("conversion", "{{AggregateMetrics.conversionRate}}", true))),

            Action("PauseCampaign", pause, Schema(("pausedAt", "string")),
                Input(("campaignId", "{{CampaignLaunch.campaignId}}", true)), nextStepId: alertMgr),
            Action("AlertManager", alertMgr, Schema(("alertId", "string")),
                Input(("conversion", "{{AggregateMetrics.conversionRate}}", true)), nextStepId: analyzeFailure),
            Action("AnalyzeFailure", analyzeFailure, Schema(("analysis", "string")),
                Input(("results", "{{ProcessSegments.segmentResults}}", true))),

            Action("ArchiveCampaign", archive, Schema(("archiveId", "string")),
                Input(("campaignId", "{{CampaignLaunch.campaignId}}", true)), nextStepId: updateDash),
            Action("UpdateDashboard", updateDash, Schema(("dashUrl", "string")),
                Input(("campaignId", "{{CampaignLaunch.campaignId}}", true))),
        };

        Assert.Equal(18, steps.Count);
        var ex = Record.Exception(() => Build(steps));
        Assert.Null(ex);
    }

    #endregion

    #region 7. HR Offboarding Pipeline (21 steps)

    /// <summary>
    /// Trigger(TerminationApproved) → FetchEmployee → Parallel(
    ///   Branch1: RevokeSSO → DisableVPN → ArchiveEmail,
    ///   Branch2: CalcFinalPay → ProcessSeverance → GeneratePayslip,
    ///   Branch3: Loop(each asset: CreateReturnLabel → SchedulePickup)
    /// ) → MergeOffboarding → SendExitPackage → ScheduleExitInterview
    /// → Condition(HasEquity?)
    ///   Yes → ProcessEquityVesting → TransferShares,
    ///   No  → SkipEquity
    /// → ArchiveRecord → NotifyHR
    /// </summary>
    [Fact]
    public void Valid_07_HROffboardingPipeline_21Steps()
    {
        var trigger = Id();
        var fetchEmp = Id(); var par = Id();
        var revokeSSO = Id(); var disableVPN = Id(); var archiveEmail = Id();
        var calcPay = Id(); var processSev = Id(); var genPayslip = Id();
        var loop = Id(); var createLabel = Id(); var schedPickup = Id();
        var mergeOff = Id(); var sendExit = Id(); var schedInterview = Id();
        var cond = Id();
        var processEquity = Id(); var transferShares = Id();
        var skipEquity = Id();
        var archiveRec = Id(); var notifyHR = Id();

        var steps = new List<StepDefinition>
        {
            Trigger("TerminationApproved", trigger, fetchEmp,
                Schema(("employeeId", "string"), ("lastDay", "string"), ("hasEquity", "string"), ("assets", "array"))),
            Action("FetchEmployee", fetchEmp, Schema(("name", "string"), ("email", "string"), ("department", "string"), ("salary", "decimal")),
                Input(("empId", "{{TerminationApproved.employeeId}}", true)), nextStepId: par),

            Parallel("OffboardTasks", par, [revokeSSO, calcPay, loop], nextStepId: mergeOff),

            Action("RevokeSSO", revokeSSO, Schema(("revokedAt", "string")),
                Input(("email", "{{FetchEmployee.email}}", true)), nextStepId: disableVPN),
            Action("DisableVPN", disableVPN, Schema(("vpnDisabled", "string")),
                Input(("empId", "{{TerminationApproved.employeeId}}", true)), nextStepId: archiveEmail),
            Action("ArchiveEmail", archiveEmail, Schema(("archiveId", "string")),
                Input(("email", "{{FetchEmployee.email}}", true))),

            Action("CalcFinalPay", calcPay, Schema(("grossPay", "decimal"), ("deductions", "decimal")),
                Input(("salary", "{{FetchEmployee.salary}}", true), ("lastDay", "{{TerminationApproved.lastDay}}", true)),
                nextStepId: processSev),
            Action("ProcessSeverance", processSev, Schema(("severanceAmount", "decimal")),
                Input(("grossPay", "{{CalcFinalPay.grossPay}}", true)), nextStepId: genPayslip),
            Action("GeneratePayslip", genPayslip, Schema(("payslipUrl", "string")),
                Input(("gross", "{{CalcFinalPay.grossPay}}", true), ("severance", "{{ProcessSeverance.severanceAmount}}", true))),

            Loop("ReturnAssets", loop, new TemplateReference("{{TerminationApproved.assets}}"), createLabel,
                Schema(("returnLabels", "array"))),
            Action("CreateReturnLabel", createLabel, Schema(("labelUrl", "string")),
                Input(("empName", "{{FetchEmployee.name}}", true)), nextStepId: schedPickup),
            Action("ScheduleAssetPickup", schedPickup, Schema(("pickupDate", "string")),
                Input(("label", "{{CreateReturnLabel.labelUrl}}", true))),

            Action("MergeOffboarding", mergeOff, Schema(("summary", "string")),
                Input(("ssoRevoked", "{{RevokeSSO.revokedAt}}", true), ("payslip", "{{GeneratePayslip.payslipUrl}}", true),
                      ("assets", "{{ReturnAssets.returnLabels}}", true)),
                nextStepId: sendExit),
            Action("SendExitPackage", sendExit, Schema(("packageId", "string")),
                Input(("email", "{{FetchEmployee.email}}", true)), nextStepId: schedInterview),
            Action("ScheduleExitInterview", schedInterview, Schema(("meetingId", "string")),
                Input(("email", "{{FetchEmployee.email}}", true)), nextStepId: cond),

            Condition("HasEquity", cond,
                rules: [Rule("{{TerminationApproved.hasEquity}} == 'true'", processEquity)],
                fallbackStepId: skipEquity, nextStepId: archiveRec),

            Action("ProcessEquityVesting", processEquity, Schema(("vestedShares", "string")),
                Input(("empId", "{{TerminationApproved.employeeId}}", true)), nextStepId: transferShares),
            Action("TransferShares", transferShares, Schema(("transferId", "string")),
                Input(("shares", "{{ProcessEquityVesting.vestedShares}}", true))),

            Action("SkipEquity", skipEquity, Schema(("skipped", "string")),
                Input(("reason", "no equity", false))),

            Action("ArchiveRecord", archiveRec, Schema(("archiveRecordId", "string")),
                Input(("empId", "{{TerminationApproved.employeeId}}", true)), nextStepId: notifyHR),
            Action("NotifyHR", notifyHR, Schema(("notifId", "string")),
                Input(("name", "{{FetchEmployee.name}}", true), ("dept", "{{FetchEmployee.department}}", true))),
        };

        Assert.Equal(21, steps.Count);
        var ex = Record.Exception(() => Build(steps));
        Assert.Null(ex);
    }

    #endregion

    #region 8. IoT Sensor Monitoring Pipeline (20 steps)

    /// <summary>
    /// Trigger(SensorReading) → NormalizeReading → CheckThresholds → Condition(Anomaly?)
    ///   Critical → Parallel(
    ///     Branch1: TriggerAlarm → NotifyOperator,
    ///     Branch2: ActivateBackup → VerifyBackup
    ///   ) → LogCritical → Condition(AutoFixAvailable?)
    ///     Yes → ApplyAutoFix → VerifyAutoFix,
    ///     No  → DispatchTechnician
    ///   Normal → StoreReading → UpdateDashboard
    /// → ArchiveTelemetry
    /// </summary>
    [Fact]
    public void Valid_08_IoTSensorMonitoringPipeline_20Steps()
    {
        var trigger = Id();
        var normalize = Id(); var checkThresh = Id(); var cond1 = Id();
        var par = Id();
        var triggerAlarm = Id(); var notifyOp = Id();
        var activateBackup = Id(); var verifyBackup = Id();
        var logCritical = Id(); var cond2 = Id();
        var applyFix = Id(); var verifyFix = Id();
        var dispatch = Id();
        var storeReading = Id(); var updateDash = Id();
        var archiveTelem = Id();

        var steps = new List<StepDefinition>
        {
            Trigger("SensorReading", trigger, normalize,
                Schema(("sensorId", "string"), ("temperature", "decimal"), ("pressure", "decimal"), ("timestamp", "string"))),
            Action("NormalizeReading", normalize, Schema(("normalizedTemp", "decimal"), ("normalizedPressure", "decimal")),
                Input(("temp", "{{SensorReading.temperature}}", true), ("pressure", "{{SensorReading.pressure}}", true)),
                nextStepId: checkThresh),
            Action("CheckThresholds", checkThresh, Schema(("isAnomaly", "string"), ("severity", "string")),
                Input(("temp", "{{NormalizeReading.normalizedTemp}}", true), ("pressure", "{{NormalizeReading.normalizedPressure}}", true)),
                nextStepId: cond1),

            Condition("AnomalyRouter", cond1,
                rules: [Rule("{{CheckThresholds.isAnomaly}} == 'true'", par)],
                fallbackStepId: storeReading, nextStepId: archiveTelem),

            // Critical branch: Parallel response → nested condition
            Parallel("CriticalResponse", par, [triggerAlarm, activateBackup], nextStepId: logCritical),

            Action("TriggerAlarm", triggerAlarm, Schema(("alarmId", "string")),
                Input(("sensorId", "{{SensorReading.sensorId}}", true)), nextStepId: notifyOp),
            Action("NotifyOperator", notifyOp, Schema(("notifId", "string")),
                Input(("alarmId", "{{TriggerAlarm.alarmId}}", true), ("severity", "{{CheckThresholds.severity}}", true))),

            Action("ActivateBackup", activateBackup, Schema(("backupId", "string")),
                Input(("sensorId", "{{SensorReading.sensorId}}", true)), nextStepId: verifyBackup),
            Action("VerifyBackup", verifyBackup, Schema(("backupStatus", "string")),
                Input(("backupId", "{{ActivateBackup.backupId}}", true))),

            Action("LogCriticalEvent", logCritical, Schema(("logId", "string"), ("autoFixAvailable", "string")),
                Input(("alarmId", "{{TriggerAlarm.alarmId}}", true), ("backup", "{{VerifyBackup.backupStatus}}", true)),
                nextStepId: cond2),

            Condition("AutoFixAvailable", cond2,
                rules: [Rule("{{LogCriticalEvent.autoFixAvailable}} == 'true'", applyFix)],
                fallbackStepId: dispatch),

            Action("ApplyAutoFix", applyFix, Schema(("fixId", "string")),
                Input(("sensorId", "{{SensorReading.sensorId}}", true)), nextStepId: verifyFix),
            Action("VerifyAutoFix", verifyFix, Schema(("fixVerified", "string")),
                Input(("fixId", "{{ApplyAutoFix.fixId}}", true))),

            Action("DispatchTechnician", dispatch, Schema(("dispatchId", "string")),
                Input(("sensorId", "{{SensorReading.sensorId}}", true), ("severity", "{{CheckThresholds.severity}}", true))),

            // Normal branch
            Action("StoreReading", storeReading, Schema(("storageId", "string")),
                Input(("sensorId", "{{SensorReading.sensorId}}", true), ("temp", "{{NormalizeReading.normalizedTemp}}", true)),
                nextStepId: updateDash),
            Action("UpdateDashboard", updateDash, Schema(("dashUpdated", "string")),
                Input(("sensorId", "{{SensorReading.sensorId}}", true))),

            // Merge
            Action("ArchiveTelemetry", archiveTelem, Schema(("archiveId", "string")),
                Input(("sensorId", "{{SensorReading.sensorId}}", true), ("timestamp", "{{SensorReading.timestamp}}", true))),
        };

        Assert.Equal(17, steps.Count);
        var ex = Record.Exception(() => Build(steps));
        Assert.Null(ex);
    }

    #endregion

    #region 9. Supply Chain Management Pipeline (24 steps)

    /// <summary>
    /// Trigger(PurchaseOrder) → ValidatePO → CheckBudget → Condition(Approved?)
    ///   Yes → Parallel(
    ///     Branch1: CreateSupplierOrder → ConfirmSupplierOrder,
    ///     Branch2: ReserveWarehouseSpace → PrepareReceivingDock
    ///   ) → MergeLogistics → Loop(each lineItem: InspectQuality → UpdateInventory → RecordReceipt)
    ///   → ReconcilePO → GenerateGRN → NotifyAccounting → UpdateERP,
    ///   No → RejectPO → NotifyRequester
    /// → ArchivePO → CloseWorkflow
    /// </summary>
    [Fact]
    public void Valid_09_SupplyChainManagementPipeline_24Steps()
    {
        var trigger = Id();
        var validatePO = Id(); var checkBudget = Id(); var cond = Id();
        var par = Id();
        var createSO = Id(); var confirmSO = Id();
        var reserveWH = Id(); var prepDock = Id();
        var mergeLog = Id(); var loop = Id();
        var inspectQual = Id(); var updateInv = Id(); var recordReceipt = Id();
        var reconcilePO = Id(); var genGRN = Id(); var notifyAcct = Id(); var updateERP = Id();
        var rejectPO = Id(); var notifyReq = Id();
        var archivePO = Id(); var closeWF = Id();

        var steps = new List<StepDefinition>
        {
            Trigger("PurchaseOrderReceived", trigger, validatePO,
                Schema(("poId", "string"), ("supplierId", "string"), ("lineItems", "array"), ("totalValue", "decimal"), ("requesterId", "string"))),
            Action("ValidatePO", validatePO, Schema(("isValid", "string"), ("validationErrors", "string")),
                Input(("poId", "{{PurchaseOrderReceived.poId}}", true)), nextStepId: checkBudget),
            Action("CheckBudget", checkBudget, Schema(("withinBudget", "string"), ("remainingBudget", "decimal")),
                Input(("amount", "{{PurchaseOrderReceived.totalValue}}", true)), nextStepId: cond),

            Condition("BudgetApproval", cond,
                rules: [Rule("{{CheckBudget.withinBudget}} == 'true'", par)],
                fallbackStepId: rejectPO, nextStepId: archivePO),

            // Approved: Parallel logistics
            Parallel("CoordinateLogistics", par, [createSO, reserveWH], nextStepId: mergeLog),

            Action("CreateSupplierOrder", createSO, Schema(("soId", "string")),
                Input(("supplierId", "{{PurchaseOrderReceived.supplierId}}", true), ("items", "{{PurchaseOrderReceived.lineItems}}", true)),
                nextStepId: confirmSO),
            Action("ConfirmSupplierOrder", confirmSO, Schema(("confirmedAt", "string"), ("estimatedDelivery", "string")),
                Input(("soId", "{{CreateSupplierOrder.soId}}", true))),

            Action("ReserveWarehouseSpace", reserveWH, Schema(("reservationId", "string"), ("dockNumber", "string")),
                Input(("items", "{{PurchaseOrderReceived.lineItems}}", true)), nextStepId: prepDock),
            Action("PrepareReceivingDock", prepDock, Schema(("dockReady", "string")),
                Input(("dock", "{{ReserveWarehouseSpace.dockNumber}}", true))),

            Action("MergeLogistics", mergeLog, Schema(("logisticsReady", "string")),
                Input(("delivery", "{{ConfirmSupplierOrder.estimatedDelivery}}", true), ("dock", "{{ReserveWarehouseSpace.dockNumber}}", true)),
                nextStepId: loop),

            Loop("InspectLineItems", loop, new TemplateReference("{{PurchaseOrderReceived.lineItems}}"), inspectQual,
                Schema(("inspectionResults", "array")), nextStepId: reconcilePO),

            Action("InspectQuality", inspectQual, Schema(("passedQC", "string"), ("defectRate", "decimal")),
                Input(("dock", "{{ReserveWarehouseSpace.dockNumber}}", true)), nextStepId: updateInv),
            Action("UpdateInventory", updateInv, Schema(("newStockLevel", "string")),
                Input(("passed", "{{InspectQuality.passedQC}}", true)), nextStepId: recordReceipt),
            Action("RecordReceipt", recordReceipt, Schema(("receiptId", "string")),
                Input(("qcResult", "{{InspectQuality.passedQC}}", true))),

            Action("ReconcilePO", reconcilePO, Schema(("reconciled", "string"), ("discrepancies", "string")),
                Input(("poId", "{{PurchaseOrderReceived.poId}}", true), ("results", "{{InspectLineItems.inspectionResults}}", true)),
                nextStepId: genGRN),
            Action("GenerateGRN", genGRN, Schema(("grnId", "string")),
                Input(("poId", "{{PurchaseOrderReceived.poId}}", true)), nextStepId: notifyAcct),
            Action("NotifyAccounting", notifyAcct, Schema(("notifId", "string")),
                Input(("grnId", "{{GenerateGRN.grnId}}", true), ("amount", "{{PurchaseOrderReceived.totalValue}}", true)),
                nextStepId: updateERP),
            Action("UpdateERP", updateERP, Schema(("erpRecordId", "string")),
                Input(("poId", "{{PurchaseOrderReceived.poId}}", true), ("grnId", "{{GenerateGRN.grnId}}", true))),

            // Rejected
            Action("RejectPO", rejectPO, Schema(("rejectionId", "string")),
                Input(("poId", "{{PurchaseOrderReceived.poId}}", true), ("budget", "{{CheckBudget.remainingBudget}}", true)),
                nextStepId: notifyReq),
            Action("NotifyRequester", notifyReq, Schema(("emailId", "string")),
                Input(("requesterId", "{{PurchaseOrderReceived.requesterId}}", true))),

            // Merge
            Action("ArchivePO", archivePO, Schema(("archiveId", "string")),
                Input(("poId", "{{PurchaseOrderReceived.poId}}", true)), nextStepId: closeWF),
            Action("CloseWorkflow", closeWF, Schema(("closedAt", "string")),
                Input(("poId", "{{PurchaseOrderReceived.poId}}", true))),
        };

        Assert.Equal(22, steps.Count);
        var ex = Record.Exception(() => Build(steps));
        Assert.Null(ex);
    }

    #endregion

    #region 10. Content Moderation Pipeline with Deep Nesting (22 steps)

    /// <summary>
    /// Trigger(ContentSubmitted) → FetchContent → Parallel(
    ///   Branch1: TextAnalysis → SentimentScore,
    ///   Branch2: ImageAnalysis → Condition(HasFaces?)
    ///     Yes → BlurFaces → FlagForReview,
    ///     No  → MarkImageSafe
    /// ) → AggregateResults → Condition(Overall?)
    ///   Safe → PublishContent → IndexForSearch → NotifyAuthor,
    ///   Unsafe → QuarantineContent → NotifyModerator → LogViolation
    /// → UpdateMetrics → ArchiveDecision
    /// </summary>
    [Fact]
    public void Valid_10_ContentModerationDeepNesting_22Steps()
    {
        var trigger = Id();
        var fetchContent = Id(); var par = Id();
        var textAnalysis = Id(); var sentimentScore = Id();
        var imageAnalysis = Id(); var condFaces = Id();
        var blurFaces = Id(); var flagReview = Id();
        var markSafe = Id();
        var aggResults = Id(); var condOverall = Id();
        var publishContent = Id(); var indexSearch = Id(); var notifyAuthor = Id();
        var quarantine = Id(); var notifyMod = Id(); var logViolation = Id();
        var updateMetrics = Id(); var archiveDecision = Id();

        var steps = new List<StepDefinition>
        {
            Trigger("ContentSubmitted", trigger, fetchContent,
                Schema(("contentId", "string"), ("authorId", "string"), ("text", "string"), ("imageUrl", "string"))),
            Action("FetchContent", fetchContent, Schema(("fullText", "string"), ("imageData", "string")),
                Input(("contentId", "{{ContentSubmitted.contentId}}", true)), nextStepId: par),

            Parallel("AnalyzeContent", par, [textAnalysis, imageAnalysis], nextStepId: aggResults),

            // Text branch
            Action("TextAnalysis", textAnalysis, Schema(("toxicity", "decimal"), ("spam", "decimal")),
                Input(("text", "{{FetchContent.fullText}}", true)), nextStepId: sentimentScore),
            Action("SentimentScore", sentimentScore, Schema(("sentiment", "string"), ("confidence", "decimal")),
                Input(("text", "{{FetchContent.fullText}}", true))),

            // Image branch with nested condition
            Action("ImageAnalysis", imageAnalysis, Schema(("hasFaces", "string"), ("nsfwScore", "decimal")),
                Input(("image", "{{FetchContent.imageData}}", true)), nextStepId: condFaces),
            Condition("HasFacesCheck", condFaces,
                rules: [Rule("{{ImageAnalysis.hasFaces}} == 'true'", blurFaces)],
                fallbackStepId: markSafe),

            Action("BlurFaces", blurFaces, Schema(("blurredImageUrl", "string")),
                Input(("image", "{{FetchContent.imageData}}", true)), nextStepId: flagReview),
            Action("FlagForReview", flagReview, Schema(("flagId", "string")),
                Input(("contentId", "{{ContentSubmitted.contentId}}", true))),

            Action("MarkImageSafe", markSafe, Schema(("safetyStatus", "string")),
                Input(("contentId", "{{ContentSubmitted.contentId}}", true))),

            // Merge: aggregate from both branches
            Action("AggregateResults", aggResults, Schema(("overallSafe", "string"), ("combinedScore", "decimal")),
                Input(("toxicity", "{{TextAnalysis.toxicity}}", true), ("nsfw", "{{ImageAnalysis.nsfwScore}}", true),
                      ("sentiment", "{{SentimentScore.sentiment}}", true)),
                nextStepId: condOverall),

            Condition("OverallDecision", condOverall,
                rules: [Rule("{{AggregateResults.overallSafe}} == 'true'", publishContent)],
                fallbackStepId: quarantine, nextStepId: updateMetrics),

            // Safe branch
            Action("PublishContent", publishContent, Schema(("publishedUrl", "string")),
                Input(("contentId", "{{ContentSubmitted.contentId}}", true)), nextStepId: indexSearch),
            Action("IndexForSearch", indexSearch, Schema(("indexId", "string")),
                Input(("url", "{{PublishContent.publishedUrl}}", true)), nextStepId: notifyAuthor),
            Action("NotifyAuthor", notifyAuthor, Schema(("emailId", "string")),
                Input(("authorId", "{{ContentSubmitted.authorId}}", true), ("url", "{{PublishContent.publishedUrl}}", true))),

            // Unsafe branch
            Action("QuarantineContent", quarantine, Schema(("quarantineId", "string")),
                Input(("contentId", "{{ContentSubmitted.contentId}}", true)), nextStepId: notifyMod),
            Action("NotifyModerator", notifyMod, Schema(("modNotifId", "string")),
                Input(("score", "{{AggregateResults.combinedScore}}", true)), nextStepId: logViolation),
            Action("LogViolation", logViolation, Schema(("violationId", "string")),
                Input(("contentId", "{{ContentSubmitted.contentId}}", true))),

            // Merge
            Action("UpdateMetrics", updateMetrics, Schema(("metricsUpdated", "string")),
                Input(("contentId", "{{ContentSubmitted.contentId}}", true)), nextStepId: archiveDecision),
            Action("ArchiveDecision", archiveDecision, Schema(("archiveId", "string")),
                Input(("contentId", "{{ContentSubmitted.contentId}}", true), ("decision", "{{AggregateResults.overallSafe}}", true))),
        };

        Assert.Equal(20, steps.Count);
        var ex = Record.Exception(() => Build(steps));
        Assert.Null(ex);
    }

    #endregion

    // =========================================================================
    // INVALID WORKFLOWS — Structural & Referencing Violations
    // =========================================================================

    #region 11-20: Invalid Workflows

    [Fact]
    public void Invalid_11_NoTrigger_ShouldThrow()
    {
        var steps = new List<StepDefinition>
        {
            Action("A", Id(), Schema(("out", "string"))),
            Action("B", Id(), Schema(("out", "string"))),
        };
        var ex = Assert.Throws<InvalidOperationException>(() => Build(steps));
        Assert.Contains("trigger", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Invalid_12_DuplicateTriggers_ShouldThrow()
    {
        var a = Id();
        var steps = new List<StepDefinition>
        {
            Trigger("T1", Id(), a, Schema(("out", "string"))),
            Trigger("T2", Id(), a, Schema(("out", "string"))),
            Action("A", a, Schema(("out", "string"))),
        };
        var ex = Assert.Throws<InvalidOperationException>(() => Build(steps));
        Assert.Contains("exactly one", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Invalid_13_CycleInGraph_ShouldThrow()
    {
        var t = Id(); var a = Id(); var b = Id(); var c = Id();
        var steps = new List<StepDefinition>
        {
            Trigger("T", t, a, Schema(("out", "string"))),
            Action("A", a, Schema(("out", "string")), nextStepId: b),
            Action("B", b, Schema(("out", "string")), nextStepId: c),
            Action("C", c, Schema(("out", "string")), nextStepId: a),
        };
        var ex = Assert.Throws<InvalidOperationException>(() => Build(steps));
        Assert.Contains("cycle", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Invalid_14_OrphanedStep_ShouldThrow()
    {
        var t = Id(); var a = Id();
        var steps = new List<StepDefinition>
        {
            Trigger("T", t, a, Schema(("out", "string"))),
            Action("A", a, Schema(("out", "string"))),
            Action("Orphan", Id(), Schema(("out", "string"))),
        };
        var ex = Assert.Throws<InvalidOperationException>(() => Build(steps));
        Assert.Contains("unreachable", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Invalid_15_DuplicateStepNames_ShouldThrow()
    {
        var t = Id(); var a = Id(); var b = Id();
        var steps = new List<StepDefinition>
        {
            Trigger("T", t, a, Schema(("out", "string"))),
            Action("Same", a, Schema(("out", "string")), nextStepId: b),
            Action("Same", b, Schema(("out", "string"))),
        };
        var ex = Assert.Throws<InvalidOperationException>(() => Build(steps));
        Assert.Contains("Duplicate", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Invalid_16_NextStepIdRefsNonExistentStep_ShouldThrow()
    {
        var t = Id(); var a = Id();
        var steps = new List<StepDefinition>
        {
            Trigger("T", t, a, Schema(("out", "string"))),
            Action("A", a, Schema(("out", "string")), nextStepId: Id()),
        };
        var ex = Assert.Throws<InvalidOperationException>(() => Build(steps));
        Assert.Contains("not found", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Invalid_17_TemplateRefsNonExistentStep_ShouldThrow()
    {
        var t = Id(); var a = Id();
        var steps = new List<StepDefinition>
        {
            Trigger("T", t, a, Schema(("sender", "string"))),
            Action("A", a, Schema(("out", "string")),
                Input(("x", "{{Ghost.field}}", true))),
        };
        var ex = Assert.Throws<InvalidOperationException>(() => Build(steps));
        Assert.Contains("unknown step", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Invalid_18_TemplateRefsNonExistentField_ShouldThrow()
    {
        var t = Id(); var a = Id();
        var steps = new List<StepDefinition>
        {
            Trigger("T", t, a, Schema(("sender", "string"))),
            Action("A", a, Schema(("out", "string")),
                Input(("x", "{{T.doesNotExist}}", true))),
        };
        var ex = Assert.Throws<InvalidOperationException>(() => Build(steps));
        Assert.Contains("non-existent field", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Parallel branch B1 tries to reference A1 in sibling branch — violation.
    /// </summary>
    [Fact]
    public void Invalid_19_ParallelBranchRefsSiblingBranch_ShouldThrow()
    {
        var t = Id(); var par = Id();
        var a1 = Id(); var a2 = Id(); var b1 = Id(); var merge = Id();
        var steps = new List<StepDefinition>
        {
            Trigger("T", t, par, Schema(("data", "string"))),
            Parallel("Fork", par, [a1, b1], nextStepId: merge),
            Action("A1", a1, Schema(("a1Out", "string")), nextStepId: a2),
            Action("A2", a2, Schema(("a2Out", "string"))),
            Action("B1", b1, Schema(("b1Out", "string")),
                Input(("x", "{{A1.a1Out}}", true))),
            Action("Merge", merge, Schema(("out", "string"))),
        };
        var ex = Assert.Throws<InvalidOperationException>(() => Build(steps));
        Assert.Contains("guaranteed to complete", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Step after condition references a step inside one of the condition branches.
    /// Only one branch runs, so the merge point can't guarantee that branch step completed.
    /// </summary>
    [Fact]
    public void Invalid_20_MergeAfterConditionRefsBranchInternal_ShouldThrow()
    {
        var t = Id(); var cond = Id();
        var b1 = Id(); var c1 = Id(); var merge = Id();
        var steps = new List<StepDefinition>
        {
            Trigger("T", t, cond, Schema(("priority", "string"))),
            Condition("Route", cond,
                rules: [Rule("{{T.priority}} == 'high'", b1)],
                fallbackStepId: c1, nextStepId: merge),
            Action("HighPath", b1, Schema(("b1Out", "string"))),
            Action("LowPath", c1, Schema(("c1Out", "string"))),
            Action("MergeStep", merge, Schema(("out", "string")),
                Input(("x", "{{HighPath.b1Out}}", true))),
        };
        var ex = Assert.Throws<InvalidOperationException>(() => Build(steps));
        Assert.Contains("guaranteed to complete", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Step after a loop references a step inside the loop body.
    /// Loop body steps are per-iteration and not available after the loop.
    /// </summary>
    [Fact]
    public void Invalid_21_StepAfterLoopRefsLoopBodyStep_ShouldThrow()
    {
        var t = Id(); var a = Id(); var loop = Id();
        var l1 = Id(); var l2 = Id(); var after = Id();
        var steps = new List<StepDefinition>
        {
            Trigger("T", t, a, Schema(("rows", "array"))),
            Action("Fetch", a, Schema(("rows", "array")), nextStepId: loop),
            Loop("MyLoop", loop, new TemplateReference("{{Fetch.rows}}"), l1,
                Schema(("results", "array")), nextStepId: after),
            Action("L1", l1, Schema(("l1Out", "string")), nextStepId: l2),
            Action("L2", l2, Schema(("l2Out", "string"))),
            Action("After", after, Schema(("out", "string")),
                Input(("x", "{{L2.l2Out}}", true))),
        };
        var ex = Assert.Throws<InvalidOperationException>(() => Build(steps));
        Assert.Contains("guaranteed to complete", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Condition branch step B references another condition branch step C.
    /// Only one branch executes so C is not guaranteed to be available.
    /// </summary>
    [Fact]
    public void Invalid_22_ConditionBranchRefsOtherBranch_ShouldThrow()
    {
        var t = Id(); var cond = Id();
        var b1 = Id(); var b2 = Id(); var c1 = Id(); var merge = Id();
        var steps = new List<StepDefinition>
        {
            Trigger("T", t, cond, Schema(("val", "string"))),
            Condition("Route", cond,
                rules: [Rule("{{T.val}} == 'a'", b1), Rule("{{T.val}} == 'b'", c1)],
                nextStepId: merge),
            Action("B1", b1, Schema(("b1Out", "string")), nextStepId: b2),
            Action("B2", b2, Schema(("b2Out", "string")),
                Input(("x", "{{C1.c1Out}}", true))),
            Action("C1", c1, Schema(("c1Out", "string"))),
            Action("Merge", merge, Schema(("out", "string"))),
        };
        var ex = Assert.Throws<InvalidOperationException>(() => Build(steps));
        Assert.Contains("guaranteed to complete", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Parallel branch has a loop, and a step AFTER the parallel merge references
    /// a step inside that loop body — double isolation violation.
    /// </summary>
    [Fact]
    public void Invalid_23_MergeAfterParallelRefsLoopBodyInsideBranch_ShouldThrow()
    {
        var t = Id(); var par = Id();
        var loop = Id(); var l1 = Id();
        var b1 = Id();
        var merge = Id();
        var steps = new List<StepDefinition>
        {
            Trigger("T", t, par, Schema(("items", "array"))),
            Parallel("Fork", par, [loop, b1], nextStepId: merge),
            Loop("LoopBranch", loop, new TemplateReference("{{T.items}}"), l1, Schema(("results", "array"))),
            Action("L1", l1, Schema(("l1Out", "string"))),
            Action("B1", b1, Schema(("b1Out", "string"))),
            Action("Merge", merge, Schema(("out", "string")),
                Input(("x", "{{L1.l1Out}}", true))),
        };
        var ex = Assert.Throws<InvalidOperationException>(() => Build(steps));
        Assert.Contains("guaranteed to complete", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Only one step (trigger with no downstream).
    /// </summary>
    [Fact]
    public void Invalid_24_OnlyTriggerStep_ShouldThrow()
    {
        var steps = new List<StepDefinition>
        {
            Trigger("T", Id(), Id(), Schema(("out", "string"))),
        };
        var ex = Assert.Throws<InvalidOperationException>(() => Build(steps));
        Assert.Contains("at least two", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    // =========================================================================
    // VALID — Edge Cases for Merge Step References
    // =========================================================================

    #region 25-30: Valid Edge Cases

    /// <summary>
    /// Merge step after parallel correctly references deep chain outputs from all branches.
    /// </summary>
    [Fact]
    public void Valid_25_MergeRefsDeepChainOutputsFromAllBranches()
    {
        var t = Id(); var par = Id();
        var a1 = Id(); var a2 = Id(); var a3 = Id();
        var b1 = Id(); var b2 = Id(); var b3 = Id();
        var merge = Id();
        var steps = new List<StepDefinition>
        {
            Trigger("T", t, par, Schema(("data", "string"))),
            Parallel("Fork", par, [a1, b1], nextStepId: merge),
            Action("A1", a1, Schema(("a1Out", "string")), nextStepId: a2),
            Action("A2", a2, Schema(("a2Out", "string")), nextStepId: a3),
            Action("A3", a3, Schema(("a3Out", "string"))),
            Action("B1", b1, Schema(("b1Out", "string")), nextStepId: b2),
            Action("B2", b2, Schema(("b2Out", "string")), nextStepId: b3),
            Action("B3", b3, Schema(("b3Out", "string"))),
            Action("Merge", merge, Schema(("combined", "string")),
                Input(("fromA3", "{{A3.a3Out}}", true), ("fromB3", "{{B3.b3Out}}", true),
                      ("fromA1", "{{A1.a1Out}}", true), ("fromB1", "{{B1.b1Out}}", true))),
        };
        var ex = Record.Exception(() => Build(steps));
        Assert.Null(ex);
    }

    /// <summary>
    /// Loop body step references trigger output (parent context) and earlier body step output.
    /// </summary>
    [Fact]
    public void Valid_26_LoopBodyRefsTriggerAndEarlierBodyStep()
    {
        var t = Id(); var fetch = Id(); var loop = Id();
        var l1 = Id(); var l2 = Id(); var l3 = Id();
        var after = Id();
        var steps = new List<StepDefinition>
        {
            Trigger("T", t, fetch, Schema(("apiKey", "string"), ("batchId", "string"))),
            Action("Fetch", fetch, Schema(("rows", "array")),
                Input(("batchId", "{{T.batchId}}", true)), nextStepId: loop),
            Loop("Process", loop, new TemplateReference("{{Fetch.rows}}"), l1,
                Schema(("processed", "array")), nextStepId: after),
            Action("L1", l1, Schema(("enriched", "string")),
                Input(("key", "{{T.apiKey}}", true)), nextStepId: l2),
            Action("L2", l2, Schema(("validated", "string")),
                Input(("data", "{{L1.enriched}}", true)), nextStepId: l3),
            Action("L3", l3, Schema(("loaded", "string")),
                Input(("data", "{{L2.validated}}", true), ("key", "{{T.apiKey}}", true))),
            Action("After", after, Schema(("summary", "string")),
                Input(("results", "{{Process.processed}}", true))),
        };
        var ex = Record.Exception(() => Build(steps));
        Assert.Null(ex);
    }

    /// <summary>
    /// Condition with 3 rules + fallback, each branch is multi-step.
    /// Merge step references only upstream, not branch internals.
    /// </summary>
    [Fact]
    public void Valid_27_ConditionWith3RulesAndFallback_NoMergeBranchRef()
    {
        var t = Id(); var classify = Id(); var cond = Id();
        var h1 = Id(); var h2 = Id();
        var m1 = Id(); var m2 = Id();
        var l1 = Id(); var l2 = Id();
        var f1 = Id();
        var merge = Id();
        var steps = new List<StepDefinition>
        {
            Trigger("T", t, classify, Schema(("text", "string"))),
            Action("Classify", classify, Schema(("category", "string"), ("confidence", "decimal")),
                Input(("text", "{{T.text}}", true)), nextStepId: cond),
            Condition("CategoryRouter", cond,
                rules: [
                    Rule("{{Classify.category}} == 'high'", h1),
                    Rule("{{Classify.category}} == 'medium'", m1),
                    Rule("{{Classify.category}} == 'low'", l1),
                ],
                fallbackStepId: f1, nextStepId: merge),
            Action("H1", h1, Schema(("h1Out", "string")), nextStepId: h2),
            Action("H2", h2, Schema(("h2Out", "string"))),
            Action("M1", m1, Schema(("m1Out", "string")), nextStepId: m2),
            Action("M2", m2, Schema(("m2Out", "string"))),
            Action("L1", l1, Schema(("l1Out", "string")), nextStepId: l2),
            Action("L2", l2, Schema(("l2Out", "string"))),
            Action("F1", f1, Schema(("f1Out", "string"))),
            Action("MergePoint", merge, Schema(("result", "string")),
                Input(("category", "{{Classify.category}}", true))),
        };
        var ex = Record.Exception(() => Build(steps));
        Assert.Null(ex);
    }

    /// <summary>
    /// Parallel inside a condition branch — deeply nested valid structure.
    /// </summary>
    [Fact]
    public void Valid_28_ParallelInsideConditionBranch()
    {
        var t = Id(); var cond = Id();
        var par = Id();
        var p1 = Id(); var p2 = Id(); var mergeP = Id();
        var fallback = Id();
        var mergeC = Id();
        var steps = new List<StepDefinition>
        {
            Trigger("T", t, cond, Schema(("mode", "string"), ("data", "string"))),
            Condition("ModeRouter", cond,
                rules: [Rule("{{T.mode}} == 'parallel'", par)],
                fallbackStepId: fallback, nextStepId: mergeC),
            Parallel("InnerFork", par, [p1, p2], nextStepId: mergeP),
            Action("P1", p1, Schema(("p1Out", "string")),
                Input(("data", "{{T.data}}", true))),
            Action("P2", p2, Schema(("p2Out", "string")),
                Input(("data", "{{T.data}}", true))),
            Action("MergeParallel", mergeP, Schema(("merged", "string")),
                Input(("fromP1", "{{P1.p1Out}}", true), ("fromP2", "{{P2.p2Out}}", true))),
            Action("Fallback", fallback, Schema(("fbOut", "string")),
                Input(("data", "{{T.data}}", true))),
            Action("MergeCondition", mergeC, Schema(("final", "string")),
                Input(("mode", "{{T.mode}}", true))),
        };
        var ex = Record.Exception(() => Build(steps));
        Assert.Null(ex);
    }

    /// <summary>
    /// Two sequential conditions — second condition references first condition's upstream.
    /// </summary>
    [Fact]
    public void Valid_29_TwoSequentialConditions()
    {
        var t = Id(); var analyze = Id(); var cond1 = Id();
        var a1 = Id(); var b1 = Id();
        var middle = Id(); var cond2 = Id();
        var c1 = Id(); var d1 = Id();
        var final_ = Id();
        var steps = new List<StepDefinition>
        {
            Trigger("T", t, analyze, Schema(("input", "string"))),
            Action("Analyze", analyze, Schema(("scoreA", "decimal"), ("scoreB", "decimal")),
                Input(("input", "{{T.input}}", true)), nextStepId: cond1),
            Condition("FirstRoute", cond1,
                rules: [Rule("{{Analyze.scoreA}} > 50", a1)],
                fallbackStepId: b1, nextStepId: middle),
            Action("A1", a1, Schema(("a1Out", "string"))),
            Action("B1", b1, Schema(("b1Out", "string"))),
            Action("Middle", middle, Schema(("middleOut", "string")),
                Input(("score", "{{Analyze.scoreA}}", true)), nextStepId: cond2),
            Condition("SecondRoute", cond2,
                rules: [Rule("{{Analyze.scoreB}} > 70", c1)],
                fallbackStepId: d1, nextStepId: final_),
            Action("C1", c1, Schema(("c1Out", "string"))),
            Action("D1", d1, Schema(("d1Out", "string"))),
            Action("Final", final_, Schema(("result", "string")),
                Input(("scoreA", "{{Analyze.scoreA}}", true), ("scoreB", "{{Analyze.scoreB}}", true))),
        };
        var ex = Record.Exception(() => Build(steps));
        Assert.Null(ex);
    }

    /// <summary>
    /// Condition step with only 1 rule + fallback — valid per requirement 8.6.7
    /// (at least one rule, not at least two).
    /// </summary>
    [Fact]
    public void Valid_30_ConditionWithOneRuleAndFallback()
    {
        var t = Id(); var cond = Id();
        var yes = Id(); var no = Id(); var merge = Id();
        var steps = new List<StepDefinition>
        {
            Trigger("T", t, cond, Schema(("flag", "string"))),
            Condition("SimpleRoute", cond,
                rules: [Rule("{{T.flag}} == 'yes'", yes)],
                fallbackStepId: no, nextStepId: merge),
            Action("YesPath", yes, Schema(("yOut", "string"))),
            Action("NoPath", no, Schema(("nOut", "string"))),
            Action("Done", merge, Schema(("result", "string")),
                Input(("flag", "{{T.flag}}", true))),
        };
        var ex = Record.Exception(() => Build(steps));
        Assert.Null(ex);
    }

    /// <summary>
    /// Deeply nested condition branch illegally points its terminal step directly
    /// at the owning condition's continuation step instead of ending at null and
    /// returning control to the condition owner.
    /// </summary>
    [Fact]
    public void Invalid_31_ConditionBranchJumpsDirectlyToOwnerContinuation_ShouldThrow()
    {
        var t = Id(); var analyze = Id(); var cond = Id();
        var highPrep = Id(); var highEnrich = Id(); var highFinalize = Id();
        var lowPrep = Id(); var lowFinalize = Id();
        var after = Id(); var archive = Id();

        var steps = new List<StepDefinition>
        {
            Trigger("T", t, analyze, Schema(("mode", "string"), ("payload", "string"))),
            Action("Analyze", analyze, Schema(("bucket", "string"), ("normalized", "string")),
                Input(("payload", "{{T.payload}}", true)), nextStepId: cond),
            Condition("Route", cond,
                rules: [Rule("{{Analyze.bucket}} == 'high'", highPrep)],
                fallbackStepId: lowPrep,
                nextStepId: after),

            Action("HighPrep", highPrep, Schema(("prep", "string")),
                Input(("payload", "{{T.payload}}", true)), nextStepId: highEnrich),
            Action("HighEnrich", highEnrich, Schema(("enriched", "string")),
                Input(("prep", "{{HighPrep.prep}}", true)), nextStepId: highFinalize),
            Action("HighFinalize", highFinalize, Schema(("done", "string")),
                Input(("data", "{{HighEnrich.enriched}}", true)), nextStepId: after),

            Action("LowPrep", lowPrep, Schema(("prep", "string")),
                Input(("payload", "{{T.payload}}", true)), nextStepId: lowFinalize),
            Action("LowFinalize", lowFinalize, Schema(("done", "string")),
                Input(("prep", "{{LowPrep.prep}}", true))),

            Action("AfterRoute", after, Schema(("result", "string")),
                Input(("bucket", "{{Analyze.bucket}}", true)), nextStepId: archive),
            Action("Archive", archive, Schema(("archiveId", "string")),
                Input(("result", "{{AfterRoute.result}}", true))),
        };

        var ex = Assert.Throws<InvalidOperationException>(() => Build(steps));
        Assert.Contains("owner continuation", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// A complex parallel branch chain illegally jumps directly into the parallel
    /// owner's continuation step. Parallel branches must terminate at null and
    /// return to the parallel owner for synchronization.
    /// </summary>
    [Fact]
    public void Invalid_32_ParallelBranchJumpsDirectlyToOwnerContinuation_ShouldThrow()
    {
        var t = Id(); var fetch = Id(); var fork = Id();
        var a1 = Id(); var a2 = Id(); var a3 = Id();
        var b1 = Id(); var b2 = Id();
        var c1 = Id(); var c2 = Id();
        var merge = Id(); var notify = Id();

        var steps = new List<StepDefinition>
        {
            Trigger("T", t, fetch, Schema(("batchId", "string"), ("payload", "string"))),
            Action("Fetch", fetch, Schema(("dataset", "string"), ("rows", "array")),
                Input(("batch", "{{T.batchId}}", true)), nextStepId: fork),
            Parallel("Fork", fork, [a1, b1, c1], nextStepId: merge),

            Action("BranchA1", a1, Schema(("a1Out", "string")),
                Input(("dataset", "{{Fetch.dataset}}", true)), nextStepId: a2),
            Action("BranchA2", a2, Schema(("a2Out", "string")),
                Input(("previous", "{{BranchA1.a1Out}}", true)), nextStepId: a3),
            Action("BranchA3", a3, Schema(("a3Out", "string")),
                Input(("previous", "{{BranchA2.a2Out}}", true)), nextStepId: merge),

            Action("BranchB1", b1, Schema(("b1Out", "string")),
                Input(("payload", "{{T.payload}}", true)), nextStepId: b2),
            Action("BranchB2", b2, Schema(("b2Out", "string")),
                Input(("previous", "{{BranchB1.b1Out}}", true))),

            Action("BranchC1", c1, Schema(("c1Out", "string")),
                Input(("dataset", "{{Fetch.dataset}}", true)), nextStepId: c2),
            Action("BranchC2", c2, Schema(("c2Out", "string")),
                Input(("previous", "{{BranchC1.c1Out}}", true))),

            Action("Merge", merge, Schema(("merged", "string")),
                Input(("a", "{{BranchA2.a2Out}}", true), ("b", "{{BranchB2.b2Out}}", true), ("c", "{{BranchC2.c2Out}}", true)),
                nextStepId: notify),
            Action("Notify", notify, Schema(("notificationId", "string")),
                Input(("merged", "{{Merge.merged}}", true))),
        };

        var ex = Assert.Throws<InvalidOperationException>(() => Build(steps));
        Assert.Contains("owner continuation", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Nested loop body chain illegally jumps directly to the loop owner's
    /// continuation step. The loop body must terminate at null so the loop owner
    /// can aggregate iteration results before continuing.
    /// </summary>
    [Fact]
    public void Invalid_33_LoopBodyJumpsDirectlyToOwnerContinuation_ShouldThrow()
    {
        var t = Id(); var fetch = Id(); var classify = Id(); var loop = Id();
        var l1 = Id(); var l2 = Id(); var l3 = Id();
        var after = Id(); var publish = Id();

        var steps = new List<StepDefinition>
        {
            Trigger("T", t, fetch, Schema(("jobId", "string"), ("items", "array"), ("tenant", "string"))),
            Action("FetchItems", fetch, Schema(("rows", "array"), ("count", "string")),
                Input(("items", "{{T.items}}", true)), nextStepId: classify),
            Action("ClassifyBatch", classify, Schema(("bucket", "string"), ("rows", "array")),
                Input(("rows", "{{FetchItems.rows}}", true)), nextStepId: loop),
            Loop("ProcessRows", loop, new TemplateReference("{{ClassifyBatch.rows}}"), l1,
                Schema(("processed", "array")), nextStepId: after),

            Action("LoopStage1", l1, Schema(("mapped", "string")),
                Input(("tenant", "{{T.tenant}}", true)), nextStepId: l2),
            Action("LoopStage2", l2, Schema(("validated", "string")),
                Input(("mapped", "{{LoopStage1.mapped}}", true)), nextStepId: l3),
            Action("LoopStage3", l3, Schema(("stored", "string")),
                Input(("validated", "{{LoopStage2.validated}}", true)), nextStepId: after),

            Action("AfterLoop", after, Schema(("summary", "string")),
                Input(("processed", "{{ProcessRows.processed}}", true)), nextStepId: publish),
            Action("Publish", publish, Schema(("publishId", "string")),
                Input(("summary", "{{AfterLoop.summary}}", true))),
        };

        var ex = Assert.Throws<InvalidOperationException>(() => Build(steps));
        Assert.Contains("owner continuation", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// A condition branch contains an inner parallel. One inner parallel branch illegally
    /// points to the parallel owner's continuation step instead of ending at null. This
    /// verifies the guard still holds when the violating owner is itself nested in a
    /// condition branch.
    /// </summary>
    [Fact]
    public void Invalid_34_ParallelInsideConditionBranch_JumpsToInnerOwnerContinuation_ShouldThrow()
    {
        var t = Id(); var gate = Id(); var cond = Id();
        var innerPar = Id();
        var p1 = Id(); var p2 = Id(); var p3 = Id();
        var innerMerge = Id(); var branchTail = Id();
        var fallback = Id();
        var afterCondition = Id(); var finalize = Id();

        var steps = new List<StepDefinition>
        {
            Trigger("T", t, gate, Schema(("mode", "string"), ("payload", "string"), ("tenant", "string"))),
            Action("Gate", gate, Schema(("route", "string"), ("normalized", "string")),
                Input(("payload", "{{T.payload}}", true)), nextStepId: cond),
            Condition("Route", cond,
                rules: [Rule("{{Gate.route}} == 'parallel'", innerPar)],
                fallbackStepId: fallback,
                nextStepId: afterCondition),

            Parallel("InnerParallel", innerPar, [p1, p2], nextStepId: innerMerge),
            Action("P1", p1, Schema(("p1Out", "string")),
                Input(("tenant", "{{T.tenant}}", true)), nextStepId: p3),
            Action("P3", p3, Schema(("p3Out", "string")),
                Input(("previous", "{{P1.p1Out}}", true)), nextStepId: innerMerge),
            Action("P2", p2, Schema(("p2Out", "string")),
                Input(("normalized", "{{Gate.normalized}}", true))),
            Action("InnerMerge", innerMerge, Schema(("merged", "string")),
                Input(("left", "{{P1.p1Out}}", true), ("right", "{{P2.p2Out}}", true)), nextStepId: branchTail),
            Action("BranchTail", branchTail, Schema(("tail", "string")),
                Input(("merged", "{{InnerMerge.merged}}", true))),

            Action("Fallback", fallback, Schema(("fb", "string")),
                Input(("payload", "{{T.payload}}", true))),

            Action("AfterCondition", afterCondition, Schema(("after", "string")),
                Input(("route", "{{Gate.route}}", true)), nextStepId: finalize),
            Action("Finalize", finalize, Schema(("done", "string")),
                Input(("after", "{{AfterCondition.after}}", true))),
        };

        var ex = Assert.Throws<InvalidOperationException>(() => Build(steps));
        Assert.Contains("owner continuation", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// A parallel branch contains a loop whose body illegally points to the loop owner's
    /// continuation step inside that branch. This guards loop-body barrier semantics even
    /// when the loop itself lives under a parallel owner.
    /// </summary>
    [Fact]
    public void Invalid_35_LoopInsideParallelBranch_JumpsToInnerOwnerContinuation_ShouldThrow()
    {
        var t = Id(); var prepare = Id(); var par = Id();
        var loop = Id(); var l1 = Id(); var l2 = Id();
        var branchAfterLoop = Id();
        var sibling1 = Id(); var sibling2 = Id();
        var merge = Id(); var publish = Id();

        var steps = new List<StepDefinition>
        {
            Trigger("T", t, prepare, Schema(("items", "array"), ("batch", "string"), ("region", "string"))),
            Action("Prepare", prepare, Schema(("rows", "array"), ("descriptor", "string")),
                Input(("items", "{{T.items}}", true)), nextStepId: par),
            Parallel("OuterParallel", par, [loop, sibling1], nextStepId: merge),

            Loop("LoopBranch", loop, new TemplateReference("{{Prepare.rows}}"), l1,
                Schema(("loopResults", "array")), nextStepId: branchAfterLoop),
            Action("LoopStage1", l1, Schema(("mapped", "string")),
                Input(("batch", "{{T.batch}}", true)), nextStepId: l2),
            Action("LoopStage2", l2, Schema(("validated", "string")),
                Input(("mapped", "{{LoopStage1.mapped}}", true)), nextStepId: branchAfterLoop),
            Action("BranchAfterLoop", branchAfterLoop, Schema(("branchSummary", "string")),
                Input(("results", "{{LoopBranch.loopResults}}", true))),

            Action("Sibling1", sibling1, Schema(("s1", "string")),
                Input(("region", "{{T.region}}", true)), nextStepId: sibling2),
            Action("Sibling2", sibling2, Schema(("s2", "string")),
                Input(("descriptor", "{{Prepare.descriptor}}", true))),

            Action("Merge", merge, Schema(("merged", "string")),
                Input(("left", "{{BranchAfterLoop.branchSummary}}", true), ("right", "{{Sibling2.s2}}", true)), nextStepId: publish),
            Action("Publish", publish, Schema(("publishId", "string")),
                Input(("merged", "{{Merge.merged}}", true))),
        };

        var ex = Assert.Throws<InvalidOperationException>(() => Build(steps));
        Assert.Contains("owner continuation", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// A condition branch contains a loop, and the loop's post-body continuation then
    /// illegally jumps directly to the owning condition's continuation step. This verifies
    /// the validator rejects bypassing two owner barriers in a nested scope.
    /// </summary>
    [Fact]
    public void Invalid_36_LoopInsideConditionBranch_JumpsToOuterOwnerContinuation_ShouldThrow()
    {
        var t = Id(); var analyze = Id(); var cond = Id();
        var loop = Id(); var l1 = Id(); var l2 = Id();
        var branchAfterLoop = Id();
        var fallback = Id();
        var afterCondition = Id(); var archive = Id();

        var steps = new List<StepDefinition>
        {
            Trigger("T", t, analyze, Schema(("rows", "array"), ("kind", "string"), ("apiKey", "string"))),
            Action("Analyze", analyze, Schema(("route", "string"), ("rows", "array")),
                Input(("rows", "{{T.rows}}", true)), nextStepId: cond),
            Condition("Route", cond,
                rules: [Rule("{{Analyze.route}} == 'loop'", loop)],
                fallbackStepId: fallback,
                nextStepId: afterCondition),

            Loop("BranchLoop", loop, new TemplateReference("{{Analyze.rows}}"), l1,
                Schema(("processed", "array")), nextStepId: branchAfterLoop),
            Action("Loop1", l1, Schema(("mapped", "string")),
                Input(("key", "{{T.apiKey}}", true)), nextStepId: l2),
            Action("Loop2", l2, Schema(("stored", "string")),
                Input(("mapped", "{{Loop1.mapped}}", true))),
            Action("BranchAfterLoop", branchAfterLoop, Schema(("branchSummary", "string")),
                Input(("processed", "{{BranchLoop.processed}}", true)), nextStepId: afterCondition),

            Action("Fallback", fallback, Schema(("fb", "string")),
                Input(("kind", "{{T.kind}}", true))),

            Action("AfterCondition", afterCondition, Schema(("summary", "string")),
                Input(("kind", "{{T.kind}}", true)), nextStepId: archive),
            Action("Archive", archive, Schema(("archiveId", "string")),
                Input(("summary", "{{AfterCondition.summary}}", true))),
        };

        var ex = Assert.Throws<InvalidOperationException>(() => Build(steps));
        Assert.Contains("owner continuation", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Invalid_37_ConditionExpressionSyntax_ShouldThrow()
    {
        var trigger = Id();
        var route = Id();
        var yes = Id();

        var steps = new List<StepDefinition>
        {
            Trigger("T", trigger, route, Schema(("flag", "string"))),
            Condition("Route", route, [Rule("{{T.flag}} ==", yes)]),
            Action("Yes", yes, Schema(("done", "string"))),
        };

        var ex = Assert.Throws<InvalidOperationException>(() => Build(steps));
        Assert.Contains("invalid condition expression", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Invalid_38_LoopSourceMustBeWholeTemplateReference_ShouldThrow()
    {
        var ex = Assert.Throws<ArgumentException>(
            () => new TemplateReference("items: {{Fetch.rows}}"));

        Assert.Contains("single workflow reference", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    #endregion
}