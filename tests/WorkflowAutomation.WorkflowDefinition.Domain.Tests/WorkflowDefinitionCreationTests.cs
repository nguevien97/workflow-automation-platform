using WorkflowAutomation.SharedKernel.Domain.Enums;
using WorkflowAutomation.SharedKernel.Domain.Ids;
using WorkflowAutomation.WorkflowDefinition.Domain.Aggregates;
using WorkflowAutomation.WorkflowDefinition.Domain.Enums;
using WorkflowAutomation.WorkflowDefinition.Domain.Ids;
using WorkflowAutomation.WorkflowDefinition.Domain.StepDefinitions;
using WorkflowAutomation.WorkflowDefinition.Domain.ValueObjects;
using WorkflowDefinitionAggregate = WorkflowAutomation.WorkflowDefinition.Domain.Aggregates.WorkflowDefinition;

namespace WorkflowAutomation.WorkflowDefinition.Domain.Tests;

public class WorkflowDefinitionCreationTests
{
    private static StepId NewStepId() => new(Guid.NewGuid());
    private static IntegrationId NewIntegrationId() => new(Guid.NewGuid());
    private static WorkflowVersionId NewVersionId() => new(Guid.NewGuid());
    private static WorkflowId NewWorkflowId() => new(Guid.NewGuid());

    private static StepOutputSchema Schema(params (string key, string type)[] fields)
    {
        var dict = new Dictionary<string, string>();
        foreach (var (key, type) in fields)
            dict[key] = type;
        return new StepOutputSchema(dict);
    }

    /// <summary>
    /// Test 1: E-commerce order processing pipeline
    /// Trigger → Action(validate) → Condition(amount check) → [high: Action(fraud) → Action(manualReview)] 
    ///   [low: Action(autoApprove)] → Condition(stock) → [inStock: Action(reserve)] [outOfStock: Action(backorder)]
    ///   → Action(payment) → Action(confirm) → Parallel(ship + notify) → Action(ship) / Action(emailNotify) 
    ///   → Action(updateDashboard) → Action(archive) → Action(generateInvoice) → Action(completeOrder)
    /// </summary>
    [Fact]
    public void Create_EcommerceOrderProcessingPipeline_Succeeds()
    {
        // Arrange - 20 steps
        var integrationId = NewIntegrationId();

        var s1 = NewStepId();  // Trigger: OrderReceived
        var s2 = NewStepId();  // Action: ValidateOrder
        var s3 = NewStepId();  // Condition: CheckOrderAmount
        var s4 = NewStepId();  // Action: FraudCheck (high amount branch)
        var s5 = NewStepId();  // Action: ManualReview
        var s6 = NewStepId();  // Action: AutoApprove (low amount branch)
        var s7 = NewStepId();  // Action: MergeApproval
        var s8 = NewStepId();  // Condition: CheckStock
        var s9 = NewStepId();  // Action: ReserveStock (inStock)
        var s10 = NewStepId(); // Action: BackorderStock (outOfStock)
        var s11 = NewStepId(); // Action: ProcessPayment
        var s12 = NewStepId(); // Action: ConfirmOrder
        var s13 = NewStepId(); // Parallel: ShipAndNotify
        var s14 = NewStepId(); // Action: ShipOrder (branch 1)
        var s15 = NewStepId(); // Action: EmailNotification (branch 2)
        var s16 = NewStepId(); // Action: UpdateDashboard
        var s17 = NewStepId(); // Action: ArchiveOrder
        var s18 = NewStepId(); // Action: GenerateInvoice
        var s19 = NewStepId(); // Action: SendInvoice
        var s20 = NewStepId(); // Action: CompleteOrder

        var steps = new List<StepDefinition>
        {
            new TriggerStepDefinition(s1, "OrderReceived", integrationId, "order.created",
                new Dictionary<string, string> { ["channel"] = "web" }, s2,
                Schema(("orderId", "string"), ("amount", "decimal"), ("customerId", "string"), ("items", "array"))),

            new ActionStepDefinition(s2, "ValidateOrder", integrationId, "order.validate",
                new Dictionary<string, TemplateOrLiteral>
                {
                    ["orderId"] = TemplateOrLiteral.Template("{{OrderReceived.orderId}}"),
                    ["customerId"] = TemplateOrLiteral.Template("{{OrderReceived.customerId}}")
                },
                FailureStrategy.Retry, retryCount: 3,
                outputSchema: Schema(("isValid", "bool"), ("validationMessage", "string")), nextStepId: s3),

            new ConditionStepDefinition(s3, "CheckOrderAmount",
                new List<ConditionRule>
                {
                    new("{{OrderReceived.amount}} > 1000", s4),
                    new("{{OrderReceived.amount}} <= 1000", s6)
                }, nextStepId: s7),

            new ActionStepDefinition(s4, "FraudCheck", integrationId, "fraud.check",
                new Dictionary<string, TemplateOrLiteral>
                {
                    ["orderId"] = TemplateOrLiteral.Template("{{OrderReceived.orderId}}"),
                    ["amount"] = TemplateOrLiteral.Template("{{OrderReceived.amount}}")
                },
                FailureStrategy.Stop,
                outputSchema: Schema(("riskScore", "int"), ("flagged", "bool")), nextStepId: s5),

            new ActionStepDefinition(s5, "ManualReview", integrationId, "review.create",
                new Dictionary<string, TemplateOrLiteral>
                {
                    ["riskScore"] = TemplateOrLiteral.Template("{{FraudCheck.riskScore}}")
                },
                FailureStrategy.Stop,
                outputSchema: Schema(("approved", "bool"), ("reviewNote", "string"))),

            new ActionStepDefinition(s6, "AutoApprove", integrationId, "order.approve",
                new Dictionary<string, TemplateOrLiteral>
                {
                    ["orderId"] = TemplateOrLiteral.Template("{{OrderReceived.orderId}}")
                },
                FailureStrategy.Skip,
                outputSchema: Schema(("approved", "bool"), ("approvalCode", "string"))),

            new ActionStepDefinition(s7, "MergeApproval", integrationId, "approval.merge",
                new Dictionary<string, TemplateOrLiteral>
                {
                    ["orderId"] = TemplateOrLiteral.Template("{{OrderReceived.orderId}}")
                },
                FailureStrategy.Stop,
                outputSchema: Schema(("finalApproval", "bool"), ("approver", "string")), nextStepId: s8),

            new ConditionStepDefinition(s8, "CheckStock",
                new List<ConditionRule>
                {
                    new("{{ValidateOrder.isValid}} == true", s9),
                    new("{{ValidateOrder.isValid}} == false", s10)
                }, nextStepId: s11),

            new ActionStepDefinition(s9, "ReserveStock", integrationId, "stock.reserve",
                new Dictionary<string, TemplateOrLiteral>
                {
                    ["items"] = TemplateOrLiteral.Template("{{OrderReceived.items}}")
                },
                FailureStrategy.Retry, retryCount: 2,
                outputSchema: Schema(("reserved", "bool"), ("warehouse", "string"))),

            new ActionStepDefinition(s10, "BackorderStock", integrationId, "stock.backorder",
                new Dictionary<string, TemplateOrLiteral>
                {
                    ["items"] = TemplateOrLiteral.Template("{{OrderReceived.items}}")
                },
                FailureStrategy.Skip,
                outputSchema: Schema(("backorderId", "string"), ("estimatedDate", "string"))),

            new ActionStepDefinition(s11, "ProcessPayment", integrationId, "payment.process",
                new Dictionary<string, TemplateOrLiteral>
                {
                    ["amount"] = TemplateOrLiteral.Template("{{OrderReceived.amount}}"),
                    ["customerId"] = TemplateOrLiteral.Template("{{OrderReceived.customerId}}")
                },
                FailureStrategy.Retry, retryCount: 3,
                outputSchema: Schema(("transactionId", "string"), ("status", "string")), nextStepId: s12),

            new ActionStepDefinition(s12, "ConfirmOrder", integrationId, "order.confirm",
                new Dictionary<string, TemplateOrLiteral>
                {
                    ["transactionId"] = TemplateOrLiteral.Template("{{ProcessPayment.transactionId}}")
                },
                FailureStrategy.Stop,
                outputSchema: Schema(("confirmationCode", "string"), ("confirmedAt", "string")), nextStepId: s13),

            new ParallelStepDefinition(s13, "ShipAndNotify",
                new List<StepId> { s14, s15 }, nextStepId: s16),

            new ActionStepDefinition(s14, "ShipOrder", integrationId, "shipping.create",
                new Dictionary<string, TemplateOrLiteral>
                {
                    ["orderId"] = TemplateOrLiteral.Template("{{OrderReceived.orderId}}"),
                    ["confirmationCode"] = TemplateOrLiteral.Template("{{ConfirmOrder.confirmationCode}}")
                },
                FailureStrategy.Retry, retryCount: 2,
                outputSchema: Schema(("trackingNumber", "string"), ("carrier", "string"))),

            new ActionStepDefinition(s15, "EmailNotification", integrationId, "email.send",
                new Dictionary<string, TemplateOrLiteral>
                {
                    ["customerId"] = TemplateOrLiteral.Template("{{OrderReceived.customerId}}"),
                    ["confirmationCode"] = TemplateOrLiteral.Template("{{ConfirmOrder.confirmationCode}}")
                },
                FailureStrategy.Skip,
                outputSchema: Schema(("emailId", "string"), ("sentAt", "string"))),

            new ActionStepDefinition(s16, "UpdateDashboard", integrationId, "dashboard.update",
                new Dictionary<string, TemplateOrLiteral>
                {
                    ["trackingNumber"] = TemplateOrLiteral.Template("{{ShipOrder.trackingNumber}}")
                },
                FailureStrategy.Skip,
                outputSchema: Schema(("dashboardId", "string"), ("updatedAt", "string")), nextStepId: s17),

            new ActionStepDefinition(s17, "ArchiveOrder", integrationId, "order.archive",
                new Dictionary<string, TemplateOrLiteral>
                {
                    ["orderId"] = TemplateOrLiteral.Template("{{OrderReceived.orderId}}")
                },
                FailureStrategy.Skip,
                outputSchema: Schema(("archiveId", "string"), ("archivedAt", "string")), nextStepId: s18),

            new ActionStepDefinition(s18, "GenerateInvoice", integrationId, "invoice.generate",
                new Dictionary<string, TemplateOrLiteral>
                {
                    ["transactionId"] = TemplateOrLiteral.Template("{{ProcessPayment.transactionId}}"),
                    ["amount"] = TemplateOrLiteral.Template("{{OrderReceived.amount}}")
                },
                FailureStrategy.Retry, retryCount: 1,
                outputSchema: Schema(("invoiceId", "string"), ("invoiceUrl", "string")), nextStepId: s19),

            new ActionStepDefinition(s19, "SendInvoice", integrationId, "invoice.send",
                new Dictionary<string, TemplateOrLiteral>
                {
                    ["invoiceUrl"] = TemplateOrLiteral.Template("{{GenerateInvoice.invoiceUrl}}"),
                    ["customerId"] = TemplateOrLiteral.Template("{{OrderReceived.customerId}}")
                },
                FailureStrategy.Skip,
                outputSchema: Schema(("sentStatus", "string"), ("deliveryId", "string")), nextStepId: s20),

            new ActionStepDefinition(s20, "CompleteOrder", integrationId, "order.complete",
                new Dictionary<string, TemplateOrLiteral>
                {
                    ["orderId"] = TemplateOrLiteral.Template("{{OrderReceived.orderId}}"),
                    ["archiveId"] = TemplateOrLiteral.Template("{{ArchiveOrder.archiveId}}")
                },
                FailureStrategy.Stop,
                outputSchema: Schema(("completedAt", "string"), ("summary", "string")))
        };

        // Act
        var result = new WorkflowDefinitionAggregate(NewVersionId(), NewWorkflowId(), steps);

        // Assert
        Assert.NotNull(result);
    }

    /// <summary>
    /// Test 2: CI/CD deployment pipeline with parallel testing and conditional rollback
    /// Trigger → Action(clone) → Parallel(unitTests + integrationTests + lintCheck) 
    ///   → Condition(allPassed?) → [yes: Action(build) → Action(pushImage) → Condition(env) → [staging: Action(deploySt)] [prod: Action(deployPr)]
    ///   → Action(smokeTst) → Action(notify)] → [no: Action(rollback) → Action(alertTeam)]
    ///   → Action(updateJira) → Action(cleanArtifacts) → Action(logMetrics) → Action(finalize)
    /// </summary>
    [Fact]
    public void Create_CiCdDeploymentPipeline_Succeeds()
    {
        var integrationId = NewIntegrationId();

        var s1 = NewStepId();  // Trigger: PushEvent
        var s2 = NewStepId();  // Action: CloneRepo
        var s3 = NewStepId();  // Parallel: RunTests
        var s4 = NewStepId();  // Action: UnitTests (branch 1)
        var s5 = NewStepId();  // Action: IntegrationTests (branch 2)
        var s6 = NewStepId();  // Action: LintCheck (branch 3)
        var s7 = NewStepId();  // Condition: AllTestsPassed
        var s8 = NewStepId();  // Action: BuildArtifact (pass branch)
        var s9 = NewStepId();  // Action: PushDockerImage
        var s10 = NewStepId(); // Condition: SelectEnvironment
        var s11 = NewStepId(); // Action: DeployStaging
        var s12 = NewStepId(); // Action: DeployProduction
        var s13 = NewStepId(); // Action: SmokeTest
        var s14 = NewStepId(); // Action: NotifySuccess
        var s15 = NewStepId(); // Action: Rollback (fail branch)
        var s16 = NewStepId(); // Action: AlertTeam
        var s17 = NewStepId(); // Action: UpdateJira
        var s18 = NewStepId(); // Action: CleanArtifacts
        var s19 = NewStepId(); // Action: LogMetrics
        var s20 = NewStepId(); // Action: FinalizePipeline

        var steps = new List<StepDefinition>
        {
            new TriggerStepDefinition(s1, "PushEvent", integrationId, "git.push",
                new Dictionary<string, string> { ["branch"] = "main" }, s2,
                Schema(("commitHash", "string"), ("branch", "string"), ("author", "string"), ("repoUrl", "string"))),

            new ActionStepDefinition(s2, "CloneRepo", integrationId, "git.clone",
                new Dictionary<string, TemplateOrLiteral>
                {
                    ["repoUrl"] = TemplateOrLiteral.Template("{{PushEvent.repoUrl}}"),
                    ["commitHash"] = TemplateOrLiteral.Template("{{PushEvent.commitHash}}")
                },
                FailureStrategy.Retry, retryCount: 2,
                outputSchema: Schema(("workDir", "string"), ("cloneStatus", "string")), nextStepId: s3),

            new ParallelStepDefinition(s3, "RunTests",
                new List<StepId> { s4, s5, s6 }, nextStepId: s7),

            new ActionStepDefinition(s4, "UnitTests", integrationId, "test.run",
                new Dictionary<string, TemplateOrLiteral>
                {
                    ["workDir"] = TemplateOrLiteral.Template("{{CloneRepo.workDir}}"),
                    ["suite"] = TemplateOrLiteral.Literal("unit")
                },
                FailureStrategy.Stop,
                outputSchema: Schema(("passed", "bool"), ("coverage", "decimal"))),

            new ActionStepDefinition(s5, "IntegrationTests", integrationId, "test.run",
                new Dictionary<string, TemplateOrLiteral>
                {
                    ["workDir"] = TemplateOrLiteral.Template("{{CloneRepo.workDir}}"),
                    ["suite"] = TemplateOrLiteral.Literal("integration")
                },
                FailureStrategy.Stop,
                outputSchema: Schema(("passed", "bool"), ("failCount", "int"))),

            new ActionStepDefinition(s6, "LintCheck", integrationId, "lint.run",
                new Dictionary<string, TemplateOrLiteral>
                {
                    ["workDir"] = TemplateOrLiteral.Template("{{CloneRepo.workDir}}")
                },
                FailureStrategy.Skip,
                outputSchema: Schema(("passed", "bool"), ("warnings", "int"))),

            new ConditionStepDefinition(s7, "AllTestsPassed",
                new List<ConditionRule>
                {
                    new("{{UnitTests.passed}} == true && {{IntegrationTests.passed}} == true", s8),
                    new("{{UnitTests.passed}} == false || {{IntegrationTests.passed}} == false", s15)
                }, nextStepId: s17),

            new ActionStepDefinition(s8, "BuildArtifact", integrationId, "build.docker",
                new Dictionary<string, TemplateOrLiteral>
                {
                    ["workDir"] = TemplateOrLiteral.Template("{{CloneRepo.workDir}}"),
                    ["commitHash"] = TemplateOrLiteral.Template("{{PushEvent.commitHash}}")
                },
                FailureStrategy.Stop,
                outputSchema: Schema(("imageTag", "string"), ("buildId", "string")), nextStepId: s9),

            new ActionStepDefinition(s9, "PushDockerImage", integrationId, "docker.push",
                new Dictionary<string, TemplateOrLiteral>
                {
                    ["imageTag"] = TemplateOrLiteral.Template("{{BuildArtifact.imageTag}}")
                },
                FailureStrategy.Retry, retryCount: 3,
                outputSchema: Schema(("registryUrl", "string"), ("digest", "string")), nextStepId: s10),

            new ConditionStepDefinition(s10, "SelectEnvironment",
                new List<ConditionRule>
                {
                    new("{{PushEvent.branch}} == 'staging'", s11),
                    new("{{PushEvent.branch}} == 'main'", s12)
                }, nextStepId: s13),

            new ActionStepDefinition(s11, "DeployStaging", integrationId, "k8s.deploy",
                new Dictionary<string, TemplateOrLiteral>
                {
                    ["imageTag"] = TemplateOrLiteral.Template("{{BuildArtifact.imageTag}}"),
                    ["env"] = TemplateOrLiteral.Literal("staging")
                },
                FailureStrategy.Retry, retryCount: 1,
                outputSchema: Schema(("deploymentId", "string"), ("status", "string"))),

            new ActionStepDefinition(s12, "DeployProduction", integrationId, "k8s.deploy",
                new Dictionary<string, TemplateOrLiteral>
                {
                    ["imageTag"] = TemplateOrLiteral.Template("{{BuildArtifact.imageTag}}"),
                    ["env"] = TemplateOrLiteral.Literal("production")
                },
                FailureStrategy.Stop,
                outputSchema: Schema(("deploymentId", "string"), ("status", "string"))),

            new ActionStepDefinition(s13, "SmokeTest", integrationId, "test.smoke",
                new Dictionary<string, TemplateOrLiteral>
                {
                    ["commitHash"] = TemplateOrLiteral.Template("{{PushEvent.commitHash}}")
                },
                FailureStrategy.Stop,
                outputSchema: Schema(("passed", "bool"), ("responseTime", "int")), nextStepId: s14),

            new ActionStepDefinition(s14, "NotifySuccess", integrationId, "slack.send",
                new Dictionary<string, TemplateOrLiteral>
                {
                    ["message"] = TemplateOrLiteral.Template("{{PushEvent.commitHash}}"),
                    ["channel"] = TemplateOrLiteral.Literal("#deployments")
                },
                FailureStrategy.Skip,
                outputSchema: Schema(("messageId", "string"), ("sentAt", "string"))),

            new ActionStepDefinition(s15, "Rollback", integrationId, "k8s.rollback",
                new Dictionary<string, TemplateOrLiteral>
                {
                    ["commitHash"] = TemplateOrLiteral.Template("{{PushEvent.commitHash}}")
                },
                FailureStrategy.Stop,
                outputSchema: Schema(("rollbackId", "string"), ("status", "string")), nextStepId: s16),

            new ActionStepDefinition(s16, "AlertTeam", integrationId, "pagerduty.alert",
                new Dictionary<string, TemplateOrLiteral>
                {
                    ["author"] = TemplateOrLiteral.Template("{{PushEvent.author}}")
                },
                FailureStrategy.Skip,
                outputSchema: Schema(("alertId", "string"), ("severity", "string"))),

            new ActionStepDefinition(s17, "UpdateJira", integrationId, "jira.update",
                new Dictionary<string, TemplateOrLiteral>
                {
                    ["commitHash"] = TemplateOrLiteral.Template("{{PushEvent.commitHash}}")
                },
                FailureStrategy.Skip,
                outputSchema: Schema(("ticketId", "string"), ("status", "string")), nextStepId: s18),

            new ActionStepDefinition(s18, "CleanArtifacts", integrationId, "storage.clean",
                new Dictionary<string, TemplateOrLiteral>
                {
                    ["workDir"] = TemplateOrLiteral.Template("{{CloneRepo.workDir}}")
                },
                FailureStrategy.Skip,
                outputSchema: Schema(("cleaned", "bool"), ("freedSpace", "string")), nextStepId: s19),

            new ActionStepDefinition(s19, "LogMetrics", integrationId, "metrics.log",
                new Dictionary<string, TemplateOrLiteral>
                {
                    ["commitHash"] = TemplateOrLiteral.Template("{{PushEvent.commitHash}}"),
                    ["branch"] = TemplateOrLiteral.Template("{{PushEvent.branch}}")
                },
                FailureStrategy.Skip,
                outputSchema: Schema(("logId", "string"), ("timestamp", "string")), nextStepId: s20),

            new ActionStepDefinition(s20, "FinalizePipeline", integrationId, "pipeline.finalize",
                new Dictionary<string, TemplateOrLiteral>
                {
                    ["commitHash"] = TemplateOrLiteral.Template("{{PushEvent.commitHash}}")
                },
                FailureStrategy.Stop,
                outputSchema: Schema(("pipelineId", "string"), ("duration", "string")))
        };

        var result = new WorkflowDefinitionAggregate(NewVersionId(), NewWorkflowId(), steps);

        Assert.NotNull(result);
    }

    /// <summary>
    /// Test 3: Customer onboarding with loop over documents and parallel verifications
    /// Trigger → Action(createAccount) → Loop(verifyDocs) → Action(enrichProfile) → Parallel(creditCheck + identityVerify + addressVerify)
    ///   → Condition(allVerified?) → [yes: Action(activate) → Action(welcomeEmail)] [no: Action(manualReview) → Action(notifyCompliance)]
    ///   → Action(syncCRM) → Action(assignManager) → Action(scheduleOnboarding) → Action(auditLog) → Action(completeOnboarding)
    /// </summary>
    [Fact]
    public void Create_CustomerOnboardingWithLoopAndParallel_Succeeds()
    {
        var integrationId = NewIntegrationId();

        var s1 = NewStepId();  // Trigger: NewCustomerEvent
        var s2 = NewStepId();  // Action: CreateAccount
        var s3 = NewStepId();  // Loop: VerifyDocuments
        var s3Inner1 = NewStepId(); // Loop inner: Action: ScanDocument
        var s3Inner2 = NewStepId(); // Loop inner: Action: ClassifyDocument
        var s4 = NewStepId();  // Action: EnrichProfile
        var s5 = NewStepId();  // Parallel: VerificationChecks
        var s6 = NewStepId();  // Action: CreditCheck (branch 1)
        var s7 = NewStepId();  // Action: IdentityVerify (branch 2)
        var s8 = NewStepId();  // Action: AddressVerify (branch 3)
        var s9 = NewStepId();  // Condition: AllVerified
        var s10 = NewStepId(); // Action: ActivateAccount (yes branch)
        var s11 = NewStepId(); // Action: WelcomeEmail
        var s12 = NewStepId(); // Action: ManualReview (no branch)
        var s13 = NewStepId(); // Action: NotifyCompliance
        var s14 = NewStepId(); // Action: SyncCRM
        var s15 = NewStepId(); // Action: AssignManager
        var s16 = NewStepId(); // Action: ScheduleOnboarding
        var s17 = NewStepId(); // Action: AuditLog
        var s18 = NewStepId(); // Action: CompleteOnboarding

        var loopInnerSteps = new List<StepDefinition>
        {
            new ActionStepDefinition(s3Inner1, "ScanDocument", integrationId, "doc.scan",
                new Dictionary<string, TemplateOrLiteral>
                {
                    ["docId"] = TemplateOrLiteral.Literal("currentItem")
                },
                FailureStrategy.Retry, retryCount: 2,
                outputSchema: Schema(("scanResult", "string"), ("confidence", "decimal")), nextStepId: s3Inner2),

            new ActionStepDefinition(s3Inner2, "ClassifyDocument", integrationId, "doc.classify",
                new Dictionary<string, TemplateOrLiteral>
                {
                    ["scanResult"] = TemplateOrLiteral.Literal("scannedData")
                },
                FailureStrategy.Skip,
                outputSchema: Schema(("docType", "string"), ("valid", "bool")))
        };

        var steps = new List<StepDefinition>
        {
            new TriggerStepDefinition(s1, "NewCustomerEvent", integrationId, "customer.created",
                new Dictionary<string, string> { ["source"] = "web-portal" }, s2,
                Schema(("customerId", "string"), ("email", "string"), ("documents", "array"), ("tier", "string"))),

            new ActionStepDefinition(s2, "CreateAccount", integrationId, "account.create",
                new Dictionary<string, TemplateOrLiteral>
                {
                    ["customerId"] = TemplateOrLiteral.Template("{{NewCustomerEvent.customerId}}"),
                    ["email"] = TemplateOrLiteral.Template("{{NewCustomerEvent.email}}")
                },
                FailureStrategy.Stop,
                outputSchema: Schema(("accountId", "string"), ("status", "string")), nextStepId: s3),

            new LoopStepDefinition(s3, "VerifyDocuments",
                new TemplateReference("{{NewCustomerEvent.documents}}"),
                Schema(("currentItem", "string")),
                loopInnerSteps,
                ConcurrencyMode.Sequential,
                IterationFailureStrategy.Skip,
                retryCount: 0, nextStepId: s4),

            new ActionStepDefinition(s4, "EnrichProfile", integrationId, "profile.enrich",
                new Dictionary<string, TemplateOrLiteral>
                {
                    ["accountId"] = TemplateOrLiteral.Template("{{CreateAccount.accountId}}")
                },
                FailureStrategy.Skip,
                outputSchema: Schema(("enriched", "bool"), ("score", "int")), nextStepId: s5),

            new ParallelStepDefinition(s5, "VerificationChecks",
                new List<StepId> { s6, s7, s8 }, nextStepId: s9),

            new ActionStepDefinition(s6, "CreditCheck", integrationId, "credit.check",
                new Dictionary<string, TemplateOrLiteral>
                {
                    ["customerId"] = TemplateOrLiteral.Template("{{NewCustomerEvent.customerId}}")
                },
                FailureStrategy.Stop,
                outputSchema: Schema(("creditScore", "int"), ("approved", "bool"))),

            new ActionStepDefinition(s7, "IdentityVerify", integrationId, "identity.verify",
                new Dictionary<string, TemplateOrLiteral>
                {
                    ["customerId"] = TemplateOrLiteral.Template("{{NewCustomerEvent.customerId}}")
                },
                FailureStrategy.Stop,
                outputSchema: Schema(("verified", "bool"), ("method", "string"))),

            new ActionStepDefinition(s8, "AddressVerify", integrationId, "address.verify",
                new Dictionary<string, TemplateOrLiteral>
                {
                    ["customerId"] = TemplateOrLiteral.Template("{{NewCustomerEvent.customerId}}")
                },
                FailureStrategy.Stop,
                outputSchema: Schema(("verified", "bool"), ("normalizedAddress", "string"))),

            new ConditionStepDefinition(s9, "AllVerified",
                new List<ConditionRule>
                {
                    new("{{CreditCheck.approved}} == true && {{IdentityVerify.verified}} == true", s10),
                    new("{{CreditCheck.approved}} == false || {{IdentityVerify.verified}} == false", s12)
                }, nextStepId: s14),

            new ActionStepDefinition(s10, "ActivateAccount", integrationId, "account.activate",
                new Dictionary<string, TemplateOrLiteral>
                {
                    ["accountId"] = TemplateOrLiteral.Template("{{CreateAccount.accountId}}")
                },
                FailureStrategy.Stop,
                outputSchema: Schema(("activatedAt", "string"), ("status", "string")), nextStepId: s11),

            new ActionStepDefinition(s11, "WelcomeEmail", integrationId, "email.welcome",
                new Dictionary<string, TemplateOrLiteral>
                {
                    ["email"] = TemplateOrLiteral.Template("{{NewCustomerEvent.email}}")
                },
                FailureStrategy.Skip,
                outputSchema: Schema(("emailId", "string"), ("sentAt", "string"))),

            new ActionStepDefinition(s12, "ManualReview", integrationId, "review.manual",
                new Dictionary<string, TemplateOrLiteral>
                {
                    ["customerId"] = TemplateOrLiteral.Template("{{NewCustomerEvent.customerId}}"),
                    ["creditScore"] = TemplateOrLiteral.Template("{{CreditCheck.creditScore}}")
                },
                FailureStrategy.Stop,
                outputSchema: Schema(("reviewId", "string"), ("outcome", "string")), nextStepId: s13),

            new ActionStepDefinition(s13, "NotifyCompliance", integrationId, "compliance.notify",
                new Dictionary<string, TemplateOrLiteral>
                {
                    ["customerId"] = TemplateOrLiteral.Template("{{NewCustomerEvent.customerId}}")
                },
                FailureStrategy.Skip,
                outputSchema: Schema(("notificationId", "string"), ("sentAt", "string"))),

            new ActionStepDefinition(s14, "SyncCRM", integrationId, "crm.sync",
                new Dictionary<string, TemplateOrLiteral>
                {
                    ["accountId"] = TemplateOrLiteral.Template("{{CreateAccount.accountId}}")
                },
                FailureStrategy.Skip,
                outputSchema: Schema(("crmId", "string"), ("syncedAt", "string")), nextStepId: s15),

            new ActionStepDefinition(s15, "AssignManager", integrationId, "manager.assign",
                new Dictionary<string, TemplateOrLiteral>
                {
                    ["tier"] = TemplateOrLiteral.Template("{{NewCustomerEvent.tier}}")
                },
                FailureStrategy.Skip,
                outputSchema: Schema(("managerId", "string"), ("managerName", "string")), nextStepId: s16),

            new ActionStepDefinition(s16, "ScheduleOnboarding", integrationId, "calendar.schedule",
                new Dictionary<string, TemplateOrLiteral>
                {
                    ["email"] = TemplateOrLiteral.Template("{{NewCustomerEvent.email}}")
                },
                FailureStrategy.Skip,
                outputSchema: Schema(("meetingId", "string"), ("scheduledAt", "string")), nextStepId: s17),

            new ActionStepDefinition(s17, "AuditLog", integrationId, "audit.log",
                new Dictionary<string, TemplateOrLiteral>
                {
                    ["customerId"] = TemplateOrLiteral.Template("{{NewCustomerEvent.customerId}}"),
                    ["accountId"] = TemplateOrLiteral.Template("{{CreateAccount.accountId}}")
                },
                FailureStrategy.Skip,
                outputSchema: Schema(("logId", "string"), ("timestamp", "string")), nextStepId: s18),

            new ActionStepDefinition(s18, "CompleteOnboarding", integrationId, "onboarding.complete",
                new Dictionary<string, TemplateOrLiteral>
                {
                    ["customerId"] = TemplateOrLiteral.Template("{{NewCustomerEvent.customerId}}")
                },
                FailureStrategy.Stop,
                outputSchema: Schema(("completedAt", "string"), ("summary", "string")))
        };

        var result = new WorkflowDefinitionAggregate(NewVersionId(), NewWorkflowId(), steps);

        Assert.NotNull(result);
    }

    /// <summary>
    /// Test 4: Data ETL pipeline with parallel extraction, loop transformation, and conditional loading
    /// Trigger → Parallel(extractDB + extractAPI + extractCSV) → Action(mergeData)
    ///   → Loop(transform each record) → Action(validate) → Condition(dataQuality)
    ///   → [good: Action(loadWarehouse)] [bad: Action(quarantine)] → Action(index) → Action(notify)
    ///   → Action(generateReport) → Action(archiveRaw) → Action(updateCatalog) → Action(finalize)
    /// </summary>
    [Fact]
    public void Create_DataEtlPipelineWithParallelAndLoop_Succeeds()
    {
        var integrationId = NewIntegrationId();

        var s1 = NewStepId();  // Trigger: ScheduledETL
        var s2 = NewStepId();  // Parallel: ExtractSources
        var s3 = NewStepId();  // Action: ExtractDB (branch 1)
        var s4 = NewStepId();  // Action: ExtractAPI (branch 2)
        var s5 = NewStepId();  // Action: ExtractCSV (branch 3)
        var s6 = NewStepId();  // Action: MergeData
        var s7 = NewStepId();  // Loop: TransformRecords
        var s7Inner1 = NewStepId(); // Loop inner: Action: NormalizeRecord
        var s7Inner2 = NewStepId(); // Loop inner: Action: EnrichRecord
        var s8 = NewStepId();  // Action: ValidateData
        var s9 = NewStepId();  // Condition: DataQuality
        var s10 = NewStepId(); // Action: LoadWarehouse (good)
        var s11 = NewStepId(); // Action: Quarantine (bad)
        var s12 = NewStepId(); // Action: IndexData
        var s13 = NewStepId(); // Action: NotifyTeam
        var s14 = NewStepId(); // Action: GenerateReport
        var s15 = NewStepId(); // Action: ArchiveRaw
        var s16 = NewStepId(); // Action: UpdateCatalog

        var loopInnerSteps = new List<StepDefinition>
        {
            new ActionStepDefinition(s7Inner1, "NormalizeRecord", integrationId, "data.normalize",
                new Dictionary<string, TemplateOrLiteral>
                {
                    ["record"] = TemplateOrLiteral.Literal("currentRecord")
                },
                FailureStrategy.Skip,
                outputSchema: Schema(("normalized", "string"), ("transformType", "string")), nextStepId: s7Inner2),

            new ActionStepDefinition(s7Inner2, "EnrichRecord", integrationId, "data.enrich",
                new Dictionary<string, TemplateOrLiteral>
                {
                    ["normalized"] = TemplateOrLiteral.Literal("normalizedData")
                },
                FailureStrategy.Skip,
                outputSchema: Schema(("enriched", "string"), ("enrichSource", "string")))
        };

        var steps = new List<StepDefinition>
        {
            new TriggerStepDefinition(s1, "ScheduledETL", integrationId, "cron.trigger",
                new Dictionary<string, string> { ["schedule"] = "0 2 * * *" }, s2,
                Schema(("runId", "string"), ("timestamp", "string"), ("records", "array"), ("batchSize", "int"))),

            new ParallelStepDefinition(s2, "ExtractSources",
                new List<StepId> { s3, s4, s5 }, nextStepId: s6),

            new ActionStepDefinition(s3, "ExtractDB", integrationId, "db.query",
                new Dictionary<string, TemplateOrLiteral>
                {
                    ["runId"] = TemplateOrLiteral.Template("{{ScheduledETL.runId}}")
                },
                FailureStrategy.Retry, retryCount: 3,
                outputSchema: Schema(("rowCount", "int"), ("dataRef", "string"))),

            new ActionStepDefinition(s4, "ExtractAPI", integrationId, "api.fetch",
                new Dictionary<string, TemplateOrLiteral>
                {
                    ["runId"] = TemplateOrLiteral.Template("{{ScheduledETL.runId}}")
                },
                FailureStrategy.Retry, retryCount: 2,
                outputSchema: Schema(("recordCount", "int"), ("dataRef", "string"))),

            new ActionStepDefinition(s5, "ExtractCSV", integrationId, "csv.import",
                new Dictionary<string, TemplateOrLiteral>
                {
                    ["runId"] = TemplateOrLiteral.Template("{{ScheduledETL.runId}}")
                },
                FailureStrategy.Skip,
                outputSchema: Schema(("lineCount", "int"), ("dataRef", "string"))),

            new ActionStepDefinition(s6, "MergeData", integrationId, "data.merge",
                new Dictionary<string, TemplateOrLiteral>
                {
                    ["dbRef"] = TemplateOrLiteral.Template("{{ExtractDB.dataRef}}"),
                    ["apiRef"] = TemplateOrLiteral.Template("{{ExtractAPI.dataRef}}"),
                    ["csvRef"] = TemplateOrLiteral.Template("{{ExtractCSV.dataRef}}")
                },
                FailureStrategy.Stop,
                outputSchema: Schema(("mergedRef", "string"), ("totalRecords", "int")), nextStepId: s7),

            new LoopStepDefinition(s7, "TransformRecords",
                new TemplateReference("{{ScheduledETL.records}}"),
                Schema(("currentRecord", "string")),
                loopInnerSteps,
                ConcurrencyMode.Parallel,
                IterationFailureStrategy.Skip,
                retryCount: 0, nextStepId: s8, maxConcurrency: 10),

            new ActionStepDefinition(s8, "ValidateData", integrationId, "data.validate",
                new Dictionary<string, TemplateOrLiteral>
                {
                    ["mergedRef"] = TemplateOrLiteral.Template("{{MergeData.mergedRef}}")
                },
                FailureStrategy.Stop,
                outputSchema: Schema(("qualityScore", "decimal"), ("validRecords", "int")), nextStepId: s9),

            new ConditionStepDefinition(s9, "DataQuality",
                new List<ConditionRule>
                {
                    new("{{ValidateData.qualityScore}} >= 0.95", s10),
                    new("{{ValidateData.qualityScore}} < 0.95", s11)
                }, nextStepId: s12),

            new ActionStepDefinition(s10, "LoadWarehouse", integrationId, "warehouse.load",
                new Dictionary<string, TemplateOrLiteral>
                {
                    ["mergedRef"] = TemplateOrLiteral.Template("{{MergeData.mergedRef}}")
                },
                FailureStrategy.Retry, retryCount: 2,
                outputSchema: Schema(("loadedCount", "int"), ("tableRef", "string"))),

            new ActionStepDefinition(s11, "Quarantine", integrationId, "data.quarantine",
                new Dictionary<string, TemplateOrLiteral>
                {
                    ["mergedRef"] = TemplateOrLiteral.Template("{{MergeData.mergedRef}}")
                },
                FailureStrategy.Stop,
                outputSchema: Schema(("quarantinedCount", "int"), ("reason", "string"))),

            new ActionStepDefinition(s12, "IndexData", integrationId, "search.index",
                new Dictionary<string, TemplateOrLiteral>
                {
                    ["runId"] = TemplateOrLiteral.Template("{{ScheduledETL.runId}}")
                },
                FailureStrategy.Skip,
                outputSchema: Schema(("indexedCount", "int"), ("indexName", "string")), nextStepId: s13),

            new ActionStepDefinition(s13, "NotifyTeam", integrationId, "slack.notify",
                new Dictionary<string, TemplateOrLiteral>
                {
                    ["runId"] = TemplateOrLiteral.Template("{{ScheduledETL.runId}}"),
                    ["totalRecords"] = TemplateOrLiteral.Template("{{MergeData.totalRecords}}")
                },
                FailureStrategy.Skip,
                outputSchema: Schema(("messageId", "string"), ("sentAt", "string")), nextStepId: s14),

            new ActionStepDefinition(s14, "GenerateReport", integrationId, "report.generate",
                new Dictionary<string, TemplateOrLiteral>
                {
                    ["runId"] = TemplateOrLiteral.Template("{{ScheduledETL.runId}}")
                },
                FailureStrategy.Skip,
                outputSchema: Schema(("reportUrl", "string"), ("reportId", "string")), nextStepId: s15),

            new ActionStepDefinition(s15, "ArchiveRaw", integrationId, "storage.archive",
                new Dictionary<string, TemplateOrLiteral>
                {
                    ["runId"] = TemplateOrLiteral.Template("{{ScheduledETL.runId}}")
                },
                FailureStrategy.Skip,
                outputSchema: Schema(("archiveId", "string"), ("size", "string")), nextStepId: s16),

            new ActionStepDefinition(s16, "UpdateCatalog", integrationId, "catalog.update",
                new Dictionary<string, TemplateOrLiteral>
                {
                    ["runId"] = TemplateOrLiteral.Template("{{ScheduledETL.runId}}"),
                    ["reportUrl"] = TemplateOrLiteral.Template("{{GenerateReport.reportUrl}}")
                },
                FailureStrategy.Skip,
                outputSchema: Schema(("catalogId", "string"), ("updatedAt", "string")))
        };

        var result = new WorkflowDefinitionAggregate(NewVersionId(), NewWorkflowId(), steps);

        Assert.NotNull(result);
    }

    /// <summary>
    /// Test 5: Incident management with nested conditions and parallel notification
    /// Trigger → Action(classifyIncident) → Condition(severity) → [critical: Action(pageOnCall) → Parallel(restartSvc + rollback)]
    ///   [high: Action(escalate)] [low: Action(createTicket)] → Condition(resolved?) → [yes: Action(closeTicket)]
    ///   [no: Action(reEscalate)] → Action(postMortem) → Action(updateRunbook) → Action(metrics)
    ///   → Action(weeklyDigest) → Action(archiveIncident) → Action(trainModel) → Action(done)
    /// </summary>
    [Fact]
    public void Create_IncidentManagementNestedConditions_Succeeds()
    {
        var integrationId = NewIntegrationId();

        var s1 = NewStepId();  // Trigger: AlertFired
        var s2 = NewStepId();  // Action: ClassifyIncident
        var s3 = NewStepId();  // Condition: SeverityCheck
        var s4 = NewStepId();  // Action: PageOnCall (critical)
        var s5 = NewStepId();  // Parallel: CriticalActions
        var s6 = NewStepId();  // Action: RestartService (branch 1)
        var s7 = NewStepId();  // Action: RollbackDeploy (branch 2)
        var s8 = NewStepId();  // Action: Escalate (high)
        var s9 = NewStepId();  // Action: CreateTicket (low)
        var s10 = NewStepId(); // Action: InvestigateRoot
        var s11 = NewStepId(); // Condition: Resolved
        var s12 = NewStepId(); // Action: CloseTicket (yes)
        var s13 = NewStepId(); // Action: ReEscalate (no)
        var s14 = NewStepId(); // Action: PostMortem
        var s15 = NewStepId(); // Action: UpdateRunbook
        var s16 = NewStepId(); // Action: LogMetrics
        var s17 = NewStepId(); // Action: WeeklyDigest
        var s18 = NewStepId(); // Action: ArchiveIncident
        var s19 = NewStepId(); // Action: TrainModel
        var s20 = NewStepId(); // Action: FinalizeIncident

        var steps = new List<StepDefinition>
        {
            new TriggerStepDefinition(s1, "AlertFired", integrationId, "monitoring.alert",
                new Dictionary<string, string> { ["source"] = "datadog" }, s2,
                Schema(("alertId", "string"), ("severity", "string"), ("service", "string"), ("message", "string"))),

            new ActionStepDefinition(s2, "ClassifyIncident", integrationId, "incident.classify",
                new Dictionary<string, TemplateOrLiteral>
                {
                    ["alertId"] = TemplateOrLiteral.Template("{{AlertFired.alertId}}"),
                    ["message"] = TemplateOrLiteral.Template("{{AlertFired.message}}")
                },
                FailureStrategy.Stop,
                outputSchema: Schema(("incidentId", "string"), ("category", "string"), ("priority", "int")), nextStepId: s3),

            new ConditionStepDefinition(s3, "SeverityCheck",
                new List<ConditionRule>
                {
                    new("{{AlertFired.severity}} == 'critical'", s4),
                    new("{{AlertFired.severity}} == 'high'", s8),
                    new("{{AlertFired.severity}} == 'low'", s9)
                }, nextStepId: s10),

            new ActionStepDefinition(s4, "PageOnCall", integrationId, "pagerduty.page",
                new Dictionary<string, TemplateOrLiteral>
                {
                    ["service"] = TemplateOrLiteral.Template("{{AlertFired.service}}"),
                    ["incidentId"] = TemplateOrLiteral.Template("{{ClassifyIncident.incidentId}}")
                },
                FailureStrategy.Retry, retryCount: 3,
                outputSchema: Schema(("pageId", "string"), ("acknowledged", "bool")), nextStepId: s5),

            new ParallelStepDefinition(s5, "CriticalActions",
                new List<StepId> { s6, s7 }),

            new ActionStepDefinition(s6, "RestartService", integrationId, "k8s.restart",
                new Dictionary<string, TemplateOrLiteral>
                {
                    ["service"] = TemplateOrLiteral.Template("{{AlertFired.service}}")
                },
                FailureStrategy.Retry, retryCount: 2,
                outputSchema: Schema(("restartId", "string"), ("status", "string"))),

            new ActionStepDefinition(s7, "RollbackDeploy", integrationId, "deploy.rollback",
                new Dictionary<string, TemplateOrLiteral>
                {
                    ["service"] = TemplateOrLiteral.Template("{{AlertFired.service}}")
                },
                FailureStrategy.Stop,
                outputSchema: Schema(("rollbackId", "string"), ("version", "string"))),

            new ActionStepDefinition(s8, "Escalate", integrationId, "incident.escalate",
                new Dictionary<string, TemplateOrLiteral>
                {
                    ["incidentId"] = TemplateOrLiteral.Template("{{ClassifyIncident.incidentId}}")
                },
                FailureStrategy.Stop,
                outputSchema: Schema(("escalationId", "string"), ("assignee", "string"))),

            new ActionStepDefinition(s9, "CreateTicket", integrationId, "jira.create",
                new Dictionary<string, TemplateOrLiteral>
                {
                    ["incidentId"] = TemplateOrLiteral.Template("{{ClassifyIncident.incidentId}}"),
                    ["category"] = TemplateOrLiteral.Template("{{ClassifyIncident.category}}")
                },
                FailureStrategy.Stop,
                outputSchema: Schema(("ticketId", "string"), ("ticketUrl", "string"))),

            new ActionStepDefinition(s10, "InvestigateRoot", integrationId, "incident.investigate",
                new Dictionary<string, TemplateOrLiteral>
                {
                    ["incidentId"] = TemplateOrLiteral.Template("{{ClassifyIncident.incidentId}}")
                },
                FailureStrategy.Stop,
                outputSchema: Schema(("rootCause", "string"), ("resolved", "bool")), nextStepId: s11),

            new ConditionStepDefinition(s11, "Resolved",
                new List<ConditionRule>
                {
                    new("{{InvestigateRoot.resolved}} == true", s12),
                    new("{{InvestigateRoot.resolved}} == false", s13)
                }, nextStepId: s14),

            new ActionStepDefinition(s12, "CloseTicket", integrationId, "jira.close",
                new Dictionary<string, TemplateOrLiteral>
                {
                    ["incidentId"] = TemplateOrLiteral.Template("{{ClassifyIncident.incidentId}}")
                },
                FailureStrategy.Stop,
                outputSchema: Schema(("closedAt", "string"), ("resolution", "string"))),

            new ActionStepDefinition(s13, "ReEscalate", integrationId, "incident.reescalate",
                new Dictionary<string, TemplateOrLiteral>
                {
                    ["incidentId"] = TemplateOrLiteral.Template("{{ClassifyIncident.incidentId}}"),
                    ["rootCause"] = TemplateOrLiteral.Template("{{InvestigateRoot.rootCause}}")
                },
                FailureStrategy.Stop,
                outputSchema: Schema(("escalationId", "string"), ("newAssignee", "string"))),

            new ActionStepDefinition(s14, "PostMortem", integrationId, "docs.create",
                new Dictionary<string, TemplateOrLiteral>
                {
                    ["incidentId"] = TemplateOrLiteral.Template("{{ClassifyIncident.incidentId}}"),
                    ["rootCause"] = TemplateOrLiteral.Template("{{InvestigateRoot.rootCause}}")
                },
                FailureStrategy.Skip,
                outputSchema: Schema(("docId", "string"), ("docUrl", "string")), nextStepId: s15),

            new ActionStepDefinition(s15, "UpdateRunbook", integrationId, "runbook.update",
                new Dictionary<string, TemplateOrLiteral>
                {
                    ["docUrl"] = TemplateOrLiteral.Template("{{PostMortem.docUrl}}")
                },
                FailureStrategy.Skip,
                outputSchema: Schema(("runbookId", "string"), ("updatedAt", "string")), nextStepId: s16),

            new ActionStepDefinition(s16, "LogMetrics", integrationId, "metrics.log",
                new Dictionary<string, TemplateOrLiteral>
                {
                    ["incidentId"] = TemplateOrLiteral.Template("{{ClassifyIncident.incidentId}}")
                },
                FailureStrategy.Skip,
                outputSchema: Schema(("metricId", "string"), ("duration", "string")), nextStepId: s17),

            new ActionStepDefinition(s17, "WeeklyDigest", integrationId, "report.digest",
                new Dictionary<string, TemplateOrLiteral>
                {
                    ["incidentId"] = TemplateOrLiteral.Template("{{ClassifyIncident.incidentId}}")
                },
                FailureStrategy.Skip,
                outputSchema: Schema(("digestId", "string"), ("generatedAt", "string")), nextStepId: s18),

            new ActionStepDefinition(s18, "ArchiveIncident", integrationId, "incident.archive",
                new Dictionary<string, TemplateOrLiteral>
                {
                    ["incidentId"] = TemplateOrLiteral.Template("{{ClassifyIncident.incidentId}}")
                },
                FailureStrategy.Skip,
                outputSchema: Schema(("archiveId", "string"), ("archivedAt", "string")), nextStepId: s19),

            new ActionStepDefinition(s19, "TrainModel", integrationId, "ml.train",
                new Dictionary<string, TemplateOrLiteral>
                {
                    ["rootCause"] = TemplateOrLiteral.Template("{{InvestigateRoot.rootCause}}")
                },
                FailureStrategy.Skip,
                outputSchema: Schema(("modelVersion", "string"), ("accuracy", "decimal")), nextStepId: s20),

            new ActionStepDefinition(s20, "FinalizeIncident", integrationId, "incident.finalize",
                new Dictionary<string, TemplateOrLiteral>
                {
                    ["incidentId"] = TemplateOrLiteral.Template("{{ClassifyIncident.incidentId}}")
                },
                FailureStrategy.Stop,
                outputSchema: Schema(("finalizedAt", "string"), ("status", "string")))
        };

        var result = new WorkflowDefinitionAggregate(NewVersionId(), NewWorkflowId(), steps);

        Assert.NotNull(result);
    }

    /// <summary>
    /// Test 6: Marketing campaign automation with loop over segments and parallel channel delivery
    /// Trigger → Action(loadAudience) → Loop(processSegments with inner steps) → Action(buildCreatives) 
    ///   → Parallel(emailChannel + smsChannel + pushChannel) → Action(mergeResults) → Condition(performance)
    ///   → [good: Action(scaleBudget)] [bad: Action(pauseCampaign)] → Action(updateAnalytics)
    ///   → Action(reportROI) → Action(archiveCampaign) → Action(schedulNext) → Action(done)
    /// </summary>
    [Fact]
    public void Create_MarketingCampaignWithLoopAndParallelChannels_Succeeds()
    {
        var integrationId = NewIntegrationId();

        var s1 = NewStepId();  // Trigger: CampaignLaunch
        var s2 = NewStepId();  // Action: LoadAudience
        var s3 = NewStepId();  // Loop: ProcessSegments
        var s3Inner1 = NewStepId(); // Loop inner: Action: ScoreSegment
        var s3Inner2 = NewStepId(); // Loop inner: Action: PersonalizeContent
        var s4 = NewStepId();  // Action: BuildCreatives
        var s5 = NewStepId();  // Parallel: DeliverChannels
        var s6 = NewStepId();  // Action: EmailChannel (branch 1)
        var s7 = NewStepId();  // Action: SMSChannel (branch 2)
        var s8 = NewStepId();  // Action: PushChannel (branch 3)
        var s9 = NewStepId();  // Action: MergeDeliveryResults
        var s10 = NewStepId(); // Condition: PerformanceCheck
        var s11 = NewStepId(); // Action: ScaleBudget (good)
        var s12 = NewStepId(); // Action: PauseCampaign (bad)
        var s13 = NewStepId(); // Action: UpdateAnalytics
        var s14 = NewStepId(); // Action: ReportROI
        var s15 = NewStepId(); // Action: ArchiveCampaign
        var s16 = NewStepId(); // Action: ScheduleNext

        var loopInnerSteps = new List<StepDefinition>
        {
            new ActionStepDefinition(s3Inner1, "ScoreSegment", integrationId, "ml.score",
                new Dictionary<string, TemplateOrLiteral>
                {
                    ["segment"] = TemplateOrLiteral.Literal("currentSegment")
                },
                FailureStrategy.Skip,
                outputSchema: Schema(("score", "decimal"), ("tier", "string")), nextStepId: s3Inner2),

            new ActionStepDefinition(s3Inner2, "PersonalizeContent", integrationId, "content.personalize",
                new Dictionary<string, TemplateOrLiteral>
                {
                    ["tier"] = TemplateOrLiteral.Literal("premium")
                },
                FailureStrategy.Skip,
                outputSchema: Schema(("contentId", "string"), ("variant", "string")))
        };

        var steps = new List<StepDefinition>
        {
            new TriggerStepDefinition(s1, "CampaignLaunch", integrationId, "campaign.launch",
                new Dictionary<string, string> { ["platform"] = "marketing-hub" }, s2,
                Schema(("campaignId", "string"), ("budget", "decimal"), ("segments", "array"), ("startDate", "string"))),

            new ActionStepDefinition(s2, "LoadAudience", integrationId, "audience.load",
                new Dictionary<string, TemplateOrLiteral>
                {
                    ["campaignId"] = TemplateOrLiteral.Template("{{CampaignLaunch.campaignId}}")
                },
                FailureStrategy.Stop,
                outputSchema: Schema(("audienceSize", "int"), ("segmentCount", "int")), nextStepId: s3),

            new LoopStepDefinition(s3, "ProcessSegments",
                new TemplateReference("{{CampaignLaunch.segments}}"),
                Schema(("currentSegment", "string")),
                loopInnerSteps,
                ConcurrencyMode.Parallel,
                IterationFailureStrategy.Skip,
                retryCount: 0, nextStepId: s4, maxConcurrency: 5),

            new ActionStepDefinition(s4, "BuildCreatives", integrationId, "creative.build",
                new Dictionary<string, TemplateOrLiteral>
                {
                    ["campaignId"] = TemplateOrLiteral.Template("{{CampaignLaunch.campaignId}}")
                },
                FailureStrategy.Retry, retryCount: 1,
                outputSchema: Schema(("creativeIds", "array"), ("count", "int")), nextStepId: s5),

            new ParallelStepDefinition(s5, "DeliverChannels",
                new List<StepId> { s6, s7, s8 }, nextStepId: s9),

            new ActionStepDefinition(s6, "EmailChannel", integrationId, "email.blast",
                new Dictionary<string, TemplateOrLiteral>
                {
                    ["campaignId"] = TemplateOrLiteral.Template("{{CampaignLaunch.campaignId}}"),
                    ["audienceSize"] = TemplateOrLiteral.Template("{{LoadAudience.audienceSize}}")
                },
                FailureStrategy.Skip,
                outputSchema: Schema(("sent", "int"), ("opened", "int"), ("bounced", "int"))),

            new ActionStepDefinition(s7, "SMSChannel", integrationId, "sms.blast",
                new Dictionary<string, TemplateOrLiteral>
                {
                    ["campaignId"] = TemplateOrLiteral.Template("{{CampaignLaunch.campaignId}}")
                },
                FailureStrategy.Skip,
                outputSchema: Schema(("sent", "int"), ("delivered", "int"))),

            new ActionStepDefinition(s8, "PushChannel", integrationId, "push.blast",
                new Dictionary<string, TemplateOrLiteral>
                {
                    ["campaignId"] = TemplateOrLiteral.Template("{{CampaignLaunch.campaignId}}")
                },
                FailureStrategy.Skip,
                outputSchema: Schema(("sent", "int"), ("clicked", "int"))),

            new ActionStepDefinition(s9, "MergeDeliveryResults", integrationId, "delivery.merge",
                new Dictionary<string, TemplateOrLiteral>
                {
                    ["emailSent"] = TemplateOrLiteral.Template("{{EmailChannel.sent}}"),
                    ["smsSent"] = TemplateOrLiteral.Template("{{SMSChannel.sent}}"),
                    ["pushSent"] = TemplateOrLiteral.Template("{{PushChannel.sent}}")
                },
                FailureStrategy.Stop,
                outputSchema: Schema(("totalSent", "int"), ("conversionRate", "decimal")), nextStepId: s10),

            new ConditionStepDefinition(s10, "PerformanceCheck",
                new List<ConditionRule>
                {
                    new("{{MergeDeliveryResults.conversionRate}} >= 0.05", s11),
                    new("{{MergeDeliveryResults.conversionRate}} < 0.05", s12)
                }, nextStepId: s13),

            new ActionStepDefinition(s11, "ScaleBudget", integrationId, "budget.scale",
                new Dictionary<string, TemplateOrLiteral>
                {
                    ["budget"] = TemplateOrLiteral.Template("{{CampaignLaunch.budget}}")
                },
                FailureStrategy.Stop,
                outputSchema: Schema(("newBudget", "decimal"), ("scaleFactor", "decimal"))),

            new ActionStepDefinition(s12, "PauseCampaign", integrationId, "campaign.pause",
                new Dictionary<string, TemplateOrLiteral>
                {
                    ["campaignId"] = TemplateOrLiteral.Template("{{CampaignLaunch.campaignId}}")
                },
                FailureStrategy.Stop,
                outputSchema: Schema(("pausedAt", "string"), ("reason", "string"))),

            new ActionStepDefinition(s13, "UpdateAnalytics", integrationId, "analytics.update",
                new Dictionary<string, TemplateOrLiteral>
                {
                    ["campaignId"] = TemplateOrLiteral.Template("{{CampaignLaunch.campaignId}}"),
                    ["totalSent"] = TemplateOrLiteral.Template("{{MergeDeliveryResults.totalSent}}")
                },
                FailureStrategy.Skip,
                outputSchema: Schema(("analyticsId", "string"), ("updatedAt", "string")), nextStepId: s14),

            new ActionStepDefinition(s14, "ReportROI", integrationId, "report.roi",
                new Dictionary<string, TemplateOrLiteral>
                {
                    ["campaignId"] = TemplateOrLiteral.Template("{{CampaignLaunch.campaignId}}")
                },
                FailureStrategy.Skip,
                outputSchema: Schema(("roi", "decimal"), ("reportUrl", "string")), nextStepId: s15),

            new ActionStepDefinition(s15, "ArchiveCampaign", integrationId, "campaign.archive",
                new Dictionary<string, TemplateOrLiteral>
                {
                    ["campaignId"] = TemplateOrLiteral.Template("{{CampaignLaunch.campaignId}}")
                },
                FailureStrategy.Skip,
                outputSchema: Schema(("archiveId", "string"), ("archivedAt", "string")), nextStepId: s16),

            new ActionStepDefinition(s16, "ScheduleNext", integrationId, "campaign.schedule",
                new Dictionary<string, TemplateOrLiteral>
                {
                    ["campaignId"] = TemplateOrLiteral.Template("{{CampaignLaunch.campaignId}}"),
                    ["startDate"] = TemplateOrLiteral.Template("{{CampaignLaunch.startDate}}")
                },
                FailureStrategy.Skip,
                outputSchema: Schema(("nextCampaignId", "string"), ("scheduledAt", "string")))
        };

        var result = new WorkflowDefinitionAggregate(NewVersionId(), NewWorkflowId(), steps);

        Assert.NotNull(result);
    }

    /// <summary>
    /// Test 7: HR employee offboarding with parallel revocations, loop over assets, conditional exit interview
    /// Trigger → Action(initOffboarding) → Parallel(revokeEmail + revokeVPN + revokeBadge) → Loop(collectAssets)
    ///   → Condition(voluntaryExit?) → [yes: Action(exitInterview) → Action(feedbackSurvey)] [no: Action(legalReview)]
    ///   → Action(finalPaycheck) → Action(benefits) → Action(archiveProfile)
    ///   → Action(notifyTeam) → Action(updateOrgChart) → Action(closeCase) → Action(complianceLog) → Action(done)
    /// </summary>
    [Fact]
    public void Create_HrOffboardingWithParallelAndLoop_Succeeds()
    {
        var integrationId = NewIntegrationId();

        var s1 = NewStepId();  // Trigger: OffboardingInitiated
        var s2 = NewStepId();  // Action: InitOffboarding
        var s3 = NewStepId();  // Parallel: RevokeAccess
        var s4 = NewStepId();  // Action: RevokeEmail (branch 1)
        var s5 = NewStepId();  // Action: RevokeVPN (branch 2)
        var s6 = NewStepId();  // Action: RevokeBadge (branch 3)
        var s7 = NewStepId();  // Loop: CollectAssets
        var s7Inner1 = NewStepId(); // Loop inner: Action: TrackAsset
        var s7Inner2 = NewStepId(); // Loop inner: Action: ConfirmReturn
        var s8 = NewStepId();  // Condition: VoluntaryExit
        var s9 = NewStepId();  // Action: ExitInterview (yes)
        var s10 = NewStepId(); // Action: FeedbackSurvey
        var s11 = NewStepId(); // Action: LegalReview (no)
        var s12 = NewStepId(); // Action: FinalPaycheck
        var s13 = NewStepId(); // Action: ProcessBenefits
        var s14 = NewStepId(); // Action: ArchiveProfile
        var s15 = NewStepId(); // Action: NotifyTeam
        var s16 = NewStepId(); // Action: UpdateOrgChart
        var s17 = NewStepId(); // Action: CloseCase
        var s18 = NewStepId(); // Action: ComplianceLog

        var loopInnerSteps = new List<StepDefinition>
        {
            new ActionStepDefinition(s7Inner1, "TrackAsset", integrationId, "asset.track",
                new Dictionary<string, TemplateOrLiteral>
                {
                    ["assetId"] = TemplateOrLiteral.Literal("currentAsset")
                },
                FailureStrategy.Skip,
                outputSchema: Schema(("assetType", "string"), ("location", "string")), nextStepId: s7Inner2),

            new ActionStepDefinition(s7Inner2, "ConfirmReturn", integrationId, "asset.confirm",
                new Dictionary<string, TemplateOrLiteral>
                {
                    ["assetType"] = TemplateOrLiteral.Literal("laptop")
                },
                FailureStrategy.Skip,
                outputSchema: Schema(("returned", "bool"), ("condition", "string")))
        };

        var steps = new List<StepDefinition>
        {
            new TriggerStepDefinition(s1, "OffboardingInitiated", integrationId, "hr.offboard",
                new Dictionary<string, string> { ["source"] = "hris" }, s2,
                Schema(("employeeId", "string"), ("department", "string"), ("exitType", "string"), ("assets", "array"))),

            new ActionStepDefinition(s2, "InitOffboarding", integrationId, "offboard.init",
                new Dictionary<string, TemplateOrLiteral>
                {
                    ["employeeId"] = TemplateOrLiteral.Template("{{OffboardingInitiated.employeeId}}"),
                    ["department"] = TemplateOrLiteral.Template("{{OffboardingInitiated.department}}")
                },
                FailureStrategy.Stop,
                outputSchema: Schema(("caseId", "string"), ("lastDay", "string")), nextStepId: s3),

            new ParallelStepDefinition(s3, "RevokeAccess",
                new List<StepId> { s4, s5, s6 }, nextStepId: s7),

            new ActionStepDefinition(s4, "RevokeEmail", integrationId, "google.revoke",
                new Dictionary<string, TemplateOrLiteral>
                {
                    ["employeeId"] = TemplateOrLiteral.Template("{{OffboardingInitiated.employeeId}}")
                },
                FailureStrategy.Retry, retryCount: 2,
                outputSchema: Schema(("revoked", "bool"), ("revokedAt", "string"))),

            new ActionStepDefinition(s5, "RevokeVPN", integrationId, "vpn.revoke",
                new Dictionary<string, TemplateOrLiteral>
                {
                    ["employeeId"] = TemplateOrLiteral.Template("{{OffboardingInitiated.employeeId}}")
                },
                FailureStrategy.Retry, retryCount: 2,
                outputSchema: Schema(("revoked", "bool"), ("revokedAt", "string"))),

            new ActionStepDefinition(s6, "RevokeBadge", integrationId, "badge.revoke",
                new Dictionary<string, TemplateOrLiteral>
                {
                    ["employeeId"] = TemplateOrLiteral.Template("{{OffboardingInitiated.employeeId}}")
                },
                FailureStrategy.Skip,
                outputSchema: Schema(("revoked", "bool"), ("badgeId", "string"))),

            new LoopStepDefinition(s7, "CollectAssets",
                new TemplateReference("{{OffboardingInitiated.assets}}"),
                Schema(("currentAsset", "string")),
                loopInnerSteps,
                ConcurrencyMode.Sequential,
                IterationFailureStrategy.Skip,
                retryCount: 0, nextStepId: s8),

            new ConditionStepDefinition(s8, "VoluntaryExit",
                new List<ConditionRule>
                {
                    new("{{OffboardingInitiated.exitType}} == 'voluntary'", s9),
                    new("{{OffboardingInitiated.exitType}} == 'involuntary'", s11)
                }, nextStepId: s12),

            new ActionStepDefinition(s9, "ExitInterview", integrationId, "interview.schedule",
                new Dictionary<string, TemplateOrLiteral>
                {
                    ["employeeId"] = TemplateOrLiteral.Template("{{OffboardingInitiated.employeeId}}")
                },
                FailureStrategy.Skip,
                outputSchema: Schema(("interviewId", "string"), ("scheduledAt", "string")), nextStepId: s10),

            new ActionStepDefinition(s10, "FeedbackSurvey", integrationId, "survey.send",
                new Dictionary<string, TemplateOrLiteral>
                {
                    ["employeeId"] = TemplateOrLiteral.Template("{{OffboardingInitiated.employeeId}}")
                },
                FailureStrategy.Skip,
                outputSchema: Schema(("surveyId", "string"), ("sentAt", "string"))),

            new ActionStepDefinition(s11, "LegalReview", integrationId, "legal.review",
                new Dictionary<string, TemplateOrLiteral>
                {
                    ["employeeId"] = TemplateOrLiteral.Template("{{OffboardingInitiated.employeeId}}"),
                    ["caseId"] = TemplateOrLiteral.Template("{{InitOffboarding.caseId}}")
                },
                FailureStrategy.Stop,
                outputSchema: Schema(("reviewId", "string"), ("clearance", "bool"))),

            new ActionStepDefinition(s12, "FinalPaycheck", integrationId, "payroll.final",
                new Dictionary<string, TemplateOrLiteral>
                {
                    ["employeeId"] = TemplateOrLiteral.Template("{{OffboardingInitiated.employeeId}}"),
                    ["lastDay"] = TemplateOrLiteral.Template("{{InitOffboarding.lastDay}}")
                },
                FailureStrategy.Stop,
                outputSchema: Schema(("paycheckId", "string"), ("amount", "decimal")), nextStepId: s13),

            new ActionStepDefinition(s13, "ProcessBenefits", integrationId, "benefits.terminate",
                new Dictionary<string, TemplateOrLiteral>
                {
                    ["employeeId"] = TemplateOrLiteral.Template("{{OffboardingInitiated.employeeId}}")
                },
                FailureStrategy.Stop,
                outputSchema: Schema(("benefitId", "string"), ("cobraEligible", "bool")), nextStepId: s14),

            new ActionStepDefinition(s14, "ArchiveProfile", integrationId, "profile.archive",
                new Dictionary<string, TemplateOrLiteral>
                {
                    ["employeeId"] = TemplateOrLiteral.Template("{{OffboardingInitiated.employeeId}}")
                },
                FailureStrategy.Skip,
                outputSchema: Schema(("archiveId", "string"), ("archivedAt", "string")), nextStepId: s15),

            new ActionStepDefinition(s15, "NotifyTeam", integrationId, "slack.notify",
                new Dictionary<string, TemplateOrLiteral>
                {
                    ["department"] = TemplateOrLiteral.Template("{{OffboardingInitiated.department}}")
                },
                FailureStrategy.Skip,
                outputSchema: Schema(("messageId", "string"), ("sentAt", "string")), nextStepId: s16),

            new ActionStepDefinition(s16, "UpdateOrgChart", integrationId, "org.update",
                new Dictionary<string, TemplateOrLiteral>
                {
                    ["employeeId"] = TemplateOrLiteral.Template("{{OffboardingInitiated.employeeId}}")
                },
                FailureStrategy.Skip,
                outputSchema: Schema(("updatedAt", "string"), ("chartVersion", "string")), nextStepId: s17),

            new ActionStepDefinition(s17, "CloseCase", integrationId, "case.close",
                new Dictionary<string, TemplateOrLiteral>
                {
                    ["caseId"] = TemplateOrLiteral.Template("{{InitOffboarding.caseId}}")
                },
                FailureStrategy.Stop,
                outputSchema: Schema(("closedAt", "string"), ("status", "string")), nextStepId: s18),

            new ActionStepDefinition(s18, "ComplianceLog", integrationId, "compliance.log",
                new Dictionary<string, TemplateOrLiteral>
                {
                    ["employeeId"] = TemplateOrLiteral.Template("{{OffboardingInitiated.employeeId}}"),
                    ["caseId"] = TemplateOrLiteral.Template("{{InitOffboarding.caseId}}")
                },
                FailureStrategy.Stop,
                outputSchema: Schema(("logId", "string"), ("timestamp", "string")))
        };

        var result = new WorkflowDefinitionAggregate(NewVersionId(), NewWorkflowId(), steps);

        Assert.NotNull(result);
    }

    /// <summary>
    /// Test 8: IoT sensor monitoring with parallel data ingestion, loop anomaly detection, conditional alerting
    /// Trigger → Parallel(ingestTemp + ingestHumidity) → Action(correlate) → Loop(detectAnomalies)
    ///   → Condition(anomalyLevel) → [critical: Action(shutdownDevice) → Action(emergencyAlert)]
    ///   [warning: Action(throttleDevice)] → Action(storeTimeSeries) → Action(updateDashboard)
    ///   → Action(generateHeatmap) → Action(predictMaintenance) → Action(scheduleInspection)
    ///   → Action(exportReport) → Action(syncCloud) → Action(archiveBatch) → Action(finalize)
    /// </summary>
    [Fact]
    public void Create_IoTSensorMonitoringWithParallelAndLoop_Succeeds()
    {
        var integrationId = NewIntegrationId();

        var s1 = NewStepId();  // Trigger: SensorBatchReceived
        var s2 = NewStepId();  // Parallel: IngestStreams
        var s3 = NewStepId();  // Action: IngestTemperature (branch 1)
        var s4 = NewStepId();  // Action: IngestHumidity (branch 2)
        var s5 = NewStepId();  // Action: CorrelateData
        var s6 = NewStepId();  // Loop: DetectAnomalies
        var s6Inner1 = NewStepId(); // Loop inner: Action: AnalyzeReading
        var s6Inner2 = NewStepId(); // Loop inner: Action: ScoreAnomaly
        var s7 = NewStepId();  // Condition: AnomalyLevel
        var s8 = NewStepId();  // Action: ShutdownDevice (critical)
        var s9 = NewStepId();  // Action: EmergencyAlert
        var s10 = NewStepId(); // Action: ThrottleDevice (warning)
        var s11 = NewStepId(); // Action: StoreTimeSeries
        var s12 = NewStepId(); // Action: UpdateDashboard
        var s13 = NewStepId(); // Action: GenerateHeatmap
        var s14 = NewStepId(); // Action: PredictMaintenance
        var s15 = NewStepId(); // Action: ScheduleInspection
        var s16 = NewStepId(); // Action: ExportReport
        var s17 = NewStepId(); // Action: SyncCloud
        var s18 = NewStepId(); // Action: ArchiveBatch

        var loopInnerSteps = new List<StepDefinition>
        {
            new ActionStepDefinition(s6Inner1, "AnalyzeReading", integrationId, "sensor.analyze",
                new Dictionary<string, TemplateOrLiteral>
                {
                    ["reading"] = TemplateOrLiteral.Literal("currentReading")
                },
                FailureStrategy.Skip,
                outputSchema: Schema(("deviation", "decimal"), ("baseline", "decimal")), nextStepId: s6Inner2),

            new ActionStepDefinition(s6Inner2, "ScoreAnomaly", integrationId, "anomaly.score",
                new Dictionary<string, TemplateOrLiteral>
                {
                    ["deviation"] = TemplateOrLiteral.Literal("0.5")
                },
                FailureStrategy.Skip,
                outputSchema: Schema(("anomalyScore", "decimal"), ("classification", "string")))
        };

        var steps = new List<StepDefinition>
        {
            new TriggerStepDefinition(s1, "SensorBatchReceived", integrationId, "iot.batch",
                new Dictionary<string, string> { ["protocol"] = "mqtt" }, s2,
                Schema(("batchId", "string"), ("deviceId", "string"), ("readings", "array"), ("timestamp", "string"))),

            new ParallelStepDefinition(s2, "IngestStreams",
                new List<StepId> { s3, s4 }, nextStepId: s5),

            new ActionStepDefinition(s3, "IngestTemperature", integrationId, "timeseries.ingest",
                new Dictionary<string, TemplateOrLiteral>
                {
                    ["batchId"] = TemplateOrLiteral.Template("{{SensorBatchReceived.batchId}}"),
                    ["type"] = TemplateOrLiteral.Literal("temperature")
                },
                FailureStrategy.Retry, retryCount: 3,
                outputSchema: Schema(("recordCount", "int"), ("avgValue", "decimal"))),

            new ActionStepDefinition(s4, "IngestHumidity", integrationId, "timeseries.ingest",
                new Dictionary<string, TemplateOrLiteral>
                {
                    ["batchId"] = TemplateOrLiteral.Template("{{SensorBatchReceived.batchId}}"),
                    ["type"] = TemplateOrLiteral.Literal("humidity")
                },
                FailureStrategy.Retry, retryCount: 3,
                outputSchema: Schema(("recordCount", "int"), ("avgValue", "decimal"))),

            new ActionStepDefinition(s5, "CorrelateData", integrationId, "data.correlate",
                new Dictionary<string, TemplateOrLiteral>
                {
                    ["tempAvg"] = TemplateOrLiteral.Template("{{IngestTemperature.avgValue}}"),
                    ["humidityAvg"] = TemplateOrLiteral.Template("{{IngestHumidity.avgValue}}")
                },
                FailureStrategy.Stop,
                outputSchema: Schema(("correlationId", "string"), ("anomalyCount", "int")), nextStepId: s6),

            new LoopStepDefinition(s6, "DetectAnomalies",
                new TemplateReference("{{SensorBatchReceived.readings}}"),
                Schema(("currentReading", "string")),
                loopInnerSteps,
                ConcurrencyMode.Parallel,
                IterationFailureStrategy.Skip,
                retryCount: 0, nextStepId: s7, maxConcurrency: 20),

            new ConditionStepDefinition(s7, "AnomalyLevel",
                new List<ConditionRule>
                {
                    new("{{CorrelateData.anomalyCount}} > 10", s8),
                    new("{{CorrelateData.anomalyCount}} <= 10 && {{CorrelateData.anomalyCount}} > 0", s10)
                },
                nextStepId: s11, fallbackStepId: s11),

            new ActionStepDefinition(s8, "ShutdownDevice", integrationId, "device.shutdown",
                new Dictionary<string, TemplateOrLiteral>
                {
                    ["deviceId"] = TemplateOrLiteral.Template("{{SensorBatchReceived.deviceId}}")
                },
                FailureStrategy.Stop,
                outputSchema: Schema(("shutdownAt", "string"), ("status", "string")), nextStepId: s9),

            new ActionStepDefinition(s9, "EmergencyAlert", integrationId, "alert.emergency",
                new Dictionary<string, TemplateOrLiteral>
                {
                    ["deviceId"] = TemplateOrLiteral.Template("{{SensorBatchReceived.deviceId}}"),
                    ["anomalyCount"] = TemplateOrLiteral.Template("{{CorrelateData.anomalyCount}}")
                },
                FailureStrategy.Retry, retryCount: 3,
                outputSchema: Schema(("alertId", "string"), ("severity", "string"))),

            new ActionStepDefinition(s10, "ThrottleDevice", integrationId, "device.throttle",
                new Dictionary<string, TemplateOrLiteral>
                {
                    ["deviceId"] = TemplateOrLiteral.Template("{{SensorBatchReceived.deviceId}}")
                },
                FailureStrategy.Skip,
                outputSchema: Schema(("throttledAt", "string"), ("newRate", "int"))),

            new ActionStepDefinition(s11, "StoreTimeSeries", integrationId, "timeseries.store",
                new Dictionary<string, TemplateOrLiteral>
                {
                    ["correlationId"] = TemplateOrLiteral.Template("{{CorrelateData.correlationId}}")
                },
                FailureStrategy.Retry, retryCount: 2,
                outputSchema: Schema(("storedCount", "int"), ("partitionKey", "string")), nextStepId: s12),

            new ActionStepDefinition(s12, "UpdateDashboard", integrationId, "dashboard.refresh",
                new Dictionary<string, TemplateOrLiteral>
                {
                    ["deviceId"] = TemplateOrLiteral.Template("{{SensorBatchReceived.deviceId}}")
                },
                FailureStrategy.Skip,
                outputSchema: Schema(("dashboardId", "string"), ("refreshedAt", "string")), nextStepId: s13),

            new ActionStepDefinition(s13, "GenerateHeatmap", integrationId, "viz.heatmap",
                new Dictionary<string, TemplateOrLiteral>
                {
                    ["batchId"] = TemplateOrLiteral.Template("{{SensorBatchReceived.batchId}}")
                },
                FailureStrategy.Skip,
                outputSchema: Schema(("heatmapUrl", "string"), ("generatedAt", "string")), nextStepId: s14),

            new ActionStepDefinition(s14, "PredictMaintenance", integrationId, "ml.predict",
                new Dictionary<string, TemplateOrLiteral>
                {
                    ["deviceId"] = TemplateOrLiteral.Template("{{SensorBatchReceived.deviceId}}"),
                    ["correlationId"] = TemplateOrLiteral.Template("{{CorrelateData.correlationId}}")
                },
                FailureStrategy.Skip,
                outputSchema: Schema(("prediction", "string"), ("confidence", "decimal")), nextStepId: s15),

            new ActionStepDefinition(s15, "ScheduleInspection", integrationId, "maintenance.schedule",
                new Dictionary<string, TemplateOrLiteral>
                {
                    ["deviceId"] = TemplateOrLiteral.Template("{{SensorBatchReceived.deviceId}}"),
                    ["prediction"] = TemplateOrLiteral.Template("{{PredictMaintenance.prediction}}")
                },
                FailureStrategy.Skip,
                outputSchema: Schema(("inspectionId", "string"), ("scheduledAt", "string")), nextStepId: s16),

            new ActionStepDefinition(s16, "ExportReport", integrationId, "report.export",
                new Dictionary<string, TemplateOrLiteral>
                {
                    ["batchId"] = TemplateOrLiteral.Template("{{SensorBatchReceived.batchId}}")
                },
                FailureStrategy.Skip,
                outputSchema: Schema(("reportUrl", "string"), ("format", "string")), nextStepId: s17),

            new ActionStepDefinition(s17, "SyncCloud", integrationId, "cloud.sync",
                new Dictionary<string, TemplateOrLiteral>
                {
                    ["batchId"] = TemplateOrLiteral.Template("{{SensorBatchReceived.batchId}}")
                },
                FailureStrategy.Skip,
                outputSchema: Schema(("syncId", "string"), ("syncedAt", "string")), nextStepId: s18),

            new ActionStepDefinition(s18, "ArchiveBatch", integrationId, "batch.archive",
                new Dictionary<string, TemplateOrLiteral>
                {
                    ["batchId"] = TemplateOrLiteral.Template("{{SensorBatchReceived.batchId}}")
                },
                FailureStrategy.Skip,
                outputSchema: Schema(("archiveId", "string"), ("archivedAt", "string")))
        };

        var result = new WorkflowDefinitionAggregate(NewVersionId(), NewWorkflowId(), steps);

        Assert.NotNull(result);
    }

    /// <summary>
    /// Test 9: Supply chain management with loop over shipments, parallel supplier checks, nested conditions
    /// Trigger → Action(receivePO) → Loop(processLineItems) → Parallel(checkSupplierA + checkSupplierB)
    ///   → Condition(bestPrice?) → [supplierA: Action(orderA)] [supplierB: Action(orderB)]
    ///   → Action(consolidateOrders) → Condition(expedite?) → [yes: Action(expediteShipping)]
    ///   [no: Action(standardShipping)] → Action(trackShipment) → Action(updateInventory)
    ///   → Action(generatePOReport) → Action(notifyProcurement) → Action(reconcileInvoice)
    ///   → Action(auditTrail) → Action(closePO)
    /// </summary>
    [Fact]
    public void Create_SupplyChainManagementWithLoopAndParallel_Succeeds()
    {
        var integrationId = NewIntegrationId();

        var s1 = NewStepId();  // Trigger: PurchaseOrderReceived
        var s2 = NewStepId();  // Action: ReceivePO
        var s3 = NewStepId();  // Loop: ProcessLineItems
        var s3Inner1 = NewStepId(); // Loop inner: Action: ValidateItem
        var s3Inner2 = NewStepId(); // Loop inner: Action: CheckAvailability
        var s4 = NewStepId();  // Parallel: CheckSuppliers
        var s5 = NewStepId();  // Action: CheckSupplierA (branch 1)
        var s6 = NewStepId();  // Action: CheckSupplierB (branch 2)
        var s7 = NewStepId();  // Condition: BestPrice
        var s8 = NewStepId();  // Action: OrderFromA (supplierA is cheaper)
        var s9 = NewStepId();  // Action: OrderFromB (supplierB is cheaper)
        var s10 = NewStepId(); // Action: ConsolidateOrders
        var s11 = NewStepId(); // Condition: ExpediteCheck
        var s12 = NewStepId(); // Action: ExpediteShipping (yes)
        var s13 = NewStepId(); // Action: StandardShipping (no)
        var s14 = NewStepId(); // Action: TrackShipment
        var s15 = NewStepId(); // Action: UpdateInventory
        var s16 = NewStepId(); // Action: GeneratePOReport
        var s17 = NewStepId(); // Action: NotifyProcurement
        var s18 = NewStepId(); // Action: ReconcileInvoice
        var s19 = NewStepId(); // Action: AuditTrail
        var s20 = NewStepId(); // Action: ClosePO

        var loopInnerSteps = new List<StepDefinition>
        {
            new ActionStepDefinition(s3Inner1, "ValidateItem", integrationId, "item.validate",
                new Dictionary<string, TemplateOrLiteral>
                {
                    ["item"] = TemplateOrLiteral.Literal("currentItem")
                },
                FailureStrategy.Skip,
                outputSchema: Schema(("valid", "bool"), ("sku", "string")), nextStepId: s3Inner2),

            new ActionStepDefinition(s3Inner2, "CheckAvailability", integrationId, "inventory.check",
                new Dictionary<string, TemplateOrLiteral>
                {
                    ["sku"] = TemplateOrLiteral.Literal("SKU-001")
                },
                FailureStrategy.Skip,
                outputSchema: Schema(("available", "bool"), ("quantity", "int")))
        };

        var steps = new List<StepDefinition>
        {
            new TriggerStepDefinition(s1, "PurchaseOrderReceived", integrationId, "erp.po.created",
                new Dictionary<string, string> { ["system"] = "SAP" }, s2,
                Schema(("poId", "string"), ("vendorId", "string"), ("lineItems", "array"), ("priority", "string"))),

            new ActionStepDefinition(s2, "ReceivePO", integrationId, "po.receive",
                new Dictionary<string, TemplateOrLiteral>
                {
                    ["poId"] = TemplateOrLiteral.Template("{{PurchaseOrderReceived.poId}}"),
                    ["vendorId"] = TemplateOrLiteral.Template("{{PurchaseOrderReceived.vendorId}}")
                },
                FailureStrategy.Stop,
                outputSchema: Schema(("receivedAt", "string"), ("totalAmount", "decimal")), nextStepId: s3),

            new LoopStepDefinition(s3, "ProcessLineItems",
                new TemplateReference("{{PurchaseOrderReceived.lineItems}}"),
                Schema(("currentItem", "string")),
                loopInnerSteps,
                ConcurrencyMode.Sequential,
                IterationFailureStrategy.Skip,
                retryCount: 0, nextStepId: s4),

            new ParallelStepDefinition(s4, "CheckSuppliers",
                new List<StepId> { s5, s6 }, nextStepId: s7),

            new ActionStepDefinition(s5, "CheckSupplierA", integrationId, "supplier.quote",
                new Dictionary<string, TemplateOrLiteral>
                {
                    ["poId"] = TemplateOrLiteral.Template("{{PurchaseOrderReceived.poId}}"),
                    ["supplier"] = TemplateOrLiteral.Literal("SupplierA")
                },
                FailureStrategy.Skip,
                outputSchema: Schema(("priceA", "decimal"), ("leadTimeA", "int"))),

            new ActionStepDefinition(s6, "CheckSupplierB", integrationId, "supplier.quote",
                new Dictionary<string, TemplateOrLiteral>
                {
                    ["poId"] = TemplateOrLiteral.Template("{{PurchaseOrderReceived.poId}}"),
                    ["supplier"] = TemplateOrLiteral.Literal("SupplierB")
                },
                FailureStrategy.Skip,
                outputSchema: Schema(("priceB", "decimal"), ("leadTimeB", "int"))),

            new ConditionStepDefinition(s7, "BestPrice",
                new List<ConditionRule>
                {
                    new("{{CheckSupplierA.priceA}} <= {{CheckSupplierB.priceB}}", s8),
                    new("{{CheckSupplierA.priceA}} > {{CheckSupplierB.priceB}}", s9)
                }, nextStepId: s10),

            new ActionStepDefinition(s8, "OrderFromA", integrationId, "supplier.order",
                new Dictionary<string, TemplateOrLiteral>
                {
                    ["poId"] = TemplateOrLiteral.Template("{{PurchaseOrderReceived.poId}}"),
                    ["price"] = TemplateOrLiteral.Template("{{CheckSupplierA.priceA}}")
                },
                FailureStrategy.Retry, retryCount: 2,
                outputSchema: Schema(("orderId", "string"), ("confirmationRef", "string"))),

            new ActionStepDefinition(s9, "OrderFromB", integrationId, "supplier.order",
                new Dictionary<string, TemplateOrLiteral>
                {
                    ["poId"] = TemplateOrLiteral.Template("{{PurchaseOrderReceived.poId}}"),
                    ["price"] = TemplateOrLiteral.Template("{{CheckSupplierB.priceB}}")
                },
                FailureStrategy.Retry, retryCount: 2,
                outputSchema: Schema(("orderId", "string"), ("confirmationRef", "string"))),

            new ActionStepDefinition(s10, "ConsolidateOrders", integrationId, "order.consolidate",
                new Dictionary<string, TemplateOrLiteral>
                {
                    ["poId"] = TemplateOrLiteral.Template("{{PurchaseOrderReceived.poId}}")
                },
                FailureStrategy.Stop,
                outputSchema: Schema(("consolidatedId", "string"), ("orderCount", "int")), nextStepId: s11),

            new ConditionStepDefinition(s11, "ExpediteCheck",
                new List<ConditionRule>
                {
                    new("{{PurchaseOrderReceived.priority}} == 'urgent'", s12),
                    new("{{PurchaseOrderReceived.priority}} == 'normal'", s13)
                }, nextStepId: s14),

            new ActionStepDefinition(s12, "ExpediteShipping", integrationId, "shipping.expedite",
                new Dictionary<string, TemplateOrLiteral>
                {
                    ["consolidatedId"] = TemplateOrLiteral.Template("{{ConsolidateOrders.consolidatedId}}")
                },
                FailureStrategy.Stop,
                outputSchema: Schema(("shipmentId", "string"), ("eta", "string"))),

            new ActionStepDefinition(s13, "StandardShipping", integrationId, "shipping.standard",
                new Dictionary<string, TemplateOrLiteral>
                {
                    ["consolidatedId"] = TemplateOrLiteral.Template("{{ConsolidateOrders.consolidatedId}}")
                },
                FailureStrategy.Stop,
                outputSchema: Schema(("shipmentId", "string"), ("eta", "string"))),

            new ActionStepDefinition(s14, "TrackShipment", integrationId, "shipping.track",
                new Dictionary<string, TemplateOrLiteral>
                {
                    ["poId"] = TemplateOrLiteral.Template("{{PurchaseOrderReceived.poId}}")
                },
                FailureStrategy.Skip,
                outputSchema: Schema(("trackingId", "string"), ("location", "string")), nextStepId: s15),

            new ActionStepDefinition(s15, "UpdateInventory", integrationId, "inventory.update",
                new Dictionary<string, TemplateOrLiteral>
                {
                    ["poId"] = TemplateOrLiteral.Template("{{PurchaseOrderReceived.poId}}"),
                    ["totalAmount"] = TemplateOrLiteral.Template("{{ReceivePO.totalAmount}}")
                },
                FailureStrategy.Stop,
                outputSchema: Schema(("inventoryId", "string"), ("updatedAt", "string")), nextStepId: s16),

            new ActionStepDefinition(s16, "GeneratePOReport", integrationId, "report.po",
                new Dictionary<string, TemplateOrLiteral>
                {
                    ["poId"] = TemplateOrLiteral.Template("{{PurchaseOrderReceived.poId}}")
                },
                FailureStrategy.Skip,
                outputSchema: Schema(("reportId", "string"), ("reportUrl", "string")), nextStepId: s17),

            new ActionStepDefinition(s17, "NotifyProcurement", integrationId, "email.notify",
                new Dictionary<string, TemplateOrLiteral>
                {
                    ["poId"] = TemplateOrLiteral.Template("{{PurchaseOrderReceived.poId}}"),
                    ["reportUrl"] = TemplateOrLiteral.Template("{{GeneratePOReport.reportUrl}}")
                },
                FailureStrategy.Skip,
                outputSchema: Schema(("emailId", "string"), ("sentAt", "string")), nextStepId: s18),

            new ActionStepDefinition(s18, "ReconcileInvoice", integrationId, "invoice.reconcile",
                new Dictionary<string, TemplateOrLiteral>
                {
                    ["poId"] = TemplateOrLiteral.Template("{{PurchaseOrderReceived.poId}}"),
                    ["totalAmount"] = TemplateOrLiteral.Template("{{ReceivePO.totalAmount}}")
                },
                FailureStrategy.Stop,
                outputSchema: Schema(("invoiceId", "string"), ("matchStatus", "string")), nextStepId: s19),

            new ActionStepDefinition(s19, "AuditTrail", integrationId, "audit.log",
                new Dictionary<string, TemplateOrLiteral>
                {
                    ["poId"] = TemplateOrLiteral.Template("{{PurchaseOrderReceived.poId}}")
                },
                FailureStrategy.Skip,
                outputSchema: Schema(("auditId", "string"), ("logTimestamp", "string")), nextStepId: s20),

            new ActionStepDefinition(s20, "ClosePO", integrationId, "po.close",
                new Dictionary<string, TemplateOrLiteral>
                {
                    ["poId"] = TemplateOrLiteral.Template("{{PurchaseOrderReceived.poId}}")
                },
                FailureStrategy.Stop,
                outputSchema: Schema(("closedAt", "string"), ("finalStatus", "string")))
        };

        var result = new WorkflowDefinitionAggregate(NewVersionId(), NewWorkflowId(), steps);

        Assert.NotNull(result);
    }

    /// <summary>
    /// Test 10: Content moderation pipeline with parallel AI models, loop over flagged items, conditional escalation
    /// Trigger → Action(ingestContent) → Parallel(textAnalysis + imageAnalysis + videoAnalysis) → Action(aggregateScores)
    ///   → Condition(autoDecision?) → [safe: Action(publish)] [flagged: Loop(reviewFlags) → Condition(severity)
    ///   → [high: Action(removeContent) → Action(suspendUser)] [medium: Action(hideContent)]]
    ///   → Action(logDecision) → Action(updatePolicy) → Action(trainClassifier)
    ///   → Action(notifyCreator) → Action(generateReport) → Action(archiveAudit) → Action(done)
    /// </summary>
    [Fact]
    public void Create_ContentModerationWithParallelAIAndLoop_Succeeds()
    {
        var integrationId = NewIntegrationId();

        var s1 = NewStepId();  // Trigger: ContentSubmitted
        var s2 = NewStepId();  // Action: IngestContent
        var s3 = NewStepId();  // Parallel: AIAnalysis
        var s4 = NewStepId();  // Action: TextAnalysis (branch 1)
        var s5 = NewStepId();  // Action: ImageAnalysis (branch 2)
        var s6 = NewStepId();  // Action: VideoAnalysis (branch 3)
        var s7 = NewStepId();  // Action: AggregateScores
        var s8 = NewStepId();  // Condition: AutoDecision
        var s9 = NewStepId();  // Action: Publish (safe)
        var s10 = NewStepId(); // Loop: ReviewFlags (flagged)
        var s10Inner1 = NewStepId(); // Loop inner: Action: EvaluateFlag
        var s10Inner2 = NewStepId(); // Loop inner: Action: RecordVerdict
        var s11 = NewStepId(); // Condition: SeverityCheck
        var s12 = NewStepId(); // Action: RemoveContent (high)
        var s13 = NewStepId(); // Action: SuspendUser
        var s14 = NewStepId(); // Action: HideContent (medium)
        var s15 = NewStepId(); // Action: LogDecision
        var s16 = NewStepId(); // Action: NotifyCreator
        var s17 = NewStepId(); // Action: GenerateReport
        var s18 = NewStepId(); // Action: ArchiveAudit

        var loopInnerSteps = new List<StepDefinition>
        {
            new ActionStepDefinition(s10Inner1, "EvaluateFlag", integrationId, "moderation.evaluate",
                new Dictionary<string, TemplateOrLiteral>
                {
                    ["flag"] = TemplateOrLiteral.Literal("currentFlag")
                },
                FailureStrategy.Skip,
                outputSchema: Schema(("flagType", "string"), ("confidence", "decimal")), nextStepId: s10Inner2),

            new ActionStepDefinition(s10Inner2, "RecordVerdict", integrationId, "moderation.record",
                new Dictionary<string, TemplateOrLiteral>
                {
                    ["flagType"] = TemplateOrLiteral.Literal("spam")
                },
                FailureStrategy.Skip,
                outputSchema: Schema(("verdictId", "string"), ("action", "string")))
        };

        var steps = new List<StepDefinition>
        {
            new TriggerStepDefinition(s1, "ContentSubmitted", integrationId, "content.submitted",
                new Dictionary<string, string> { ["platform"] = "social" }, s2,
                Schema(("contentId", "string"), ("userId", "string"), ("contentType", "string"), ("flags", "array"))),

            new ActionStepDefinition(s2, "IngestContent", integrationId, "content.ingest",
                new Dictionary<string, TemplateOrLiteral>
                {
                    ["contentId"] = TemplateOrLiteral.Template("{{ContentSubmitted.contentId}}"),
                    ["contentType"] = TemplateOrLiteral.Template("{{ContentSubmitted.contentType}}")
                },
                FailureStrategy.Stop,
                outputSchema: Schema(("ingestId", "string"), ("size", "int")), nextStepId: s3),

            new ParallelStepDefinition(s3, "AIAnalysis",
                new List<StepId> { s4, s5, s6 }, nextStepId: s7),

            new ActionStepDefinition(s4, "TextAnalysis", integrationId, "ai.text",
                new Dictionary<string, TemplateOrLiteral>
                {
                    ["contentId"] = TemplateOrLiteral.Template("{{ContentSubmitted.contentId}}")
                },
                FailureStrategy.Skip,
                outputSchema: Schema(("toxicityScore", "decimal"), ("sentiment", "string"))),

            new ActionStepDefinition(s5, "ImageAnalysis", integrationId, "ai.image",
                new Dictionary<string, TemplateOrLiteral>
                {
                    ["contentId"] = TemplateOrLiteral.Template("{{ContentSubmitted.contentId}}")
                },
                FailureStrategy.Skip,
                outputSchema: Schema(("nsfwScore", "decimal"), ("objectDetection", "string"))),

            new ActionStepDefinition(s6, "VideoAnalysis", integrationId, "ai.video",
                new Dictionary<string, TemplateOrLiteral>
                {
                    ["contentId"] = TemplateOrLiteral.Template("{{ContentSubmitted.contentId}}")
                },
                FailureStrategy.Skip,
                outputSchema: Schema(("violenceScore", "decimal"), ("duration", "int"))),

            new ActionStepDefinition(s7, "AggregateScores", integrationId, "moderation.aggregate",
                new Dictionary<string, TemplateOrLiteral>
                {
                    ["toxicity"] = TemplateOrLiteral.Template("{{TextAnalysis.toxicityScore}}"),
                    ["nsfw"] = TemplateOrLiteral.Template("{{ImageAnalysis.nsfwScore}}"),
                    ["violence"] = TemplateOrLiteral.Template("{{VideoAnalysis.violenceScore}}")
                },
                FailureStrategy.Stop,
                outputSchema: Schema(("overallScore", "decimal"), ("decision", "string"), ("severity", "string")), nextStepId: s8),

            new ConditionStepDefinition(s8, "AutoDecision",
                new List<ConditionRule>
                {
                    new("{{AggregateScores.decision}} == 'safe'", s9),
                    new("{{AggregateScores.decision}} == 'flagged'", s10)
                }, nextStepId: s15),

            new ActionStepDefinition(s9, "Publish", integrationId, "content.publish",
                new Dictionary<string, TemplateOrLiteral>
                {
                    ["contentId"] = TemplateOrLiteral.Template("{{ContentSubmitted.contentId}}")
                },
                FailureStrategy.Stop,
                outputSchema: Schema(("publishedAt", "string"), ("url", "string"))),

            new LoopStepDefinition(s10, "ReviewFlags",
                new TemplateReference("{{ContentSubmitted.flags}}"),
                Schema(("currentFlag", "string")),
                loopInnerSteps,
                ConcurrencyMode.Sequential,
                IterationFailureStrategy.Skip,
                retryCount: 0, nextStepId: s11),

            new ConditionStepDefinition(s11, "SeverityCheck",
                new List<ConditionRule>
                {
                    new("{{AggregateScores.severity}} == 'high'", s12),
                    new("{{AggregateScores.severity}} == 'medium'", s14)
                }),

            new ActionStepDefinition(s12, "RemoveContent", integrationId, "content.remove",
                new Dictionary<string, TemplateOrLiteral>
                {
                    ["contentId"] = TemplateOrLiteral.Template("{{ContentSubmitted.contentId}}")
                },
                FailureStrategy.Stop,
                outputSchema: Schema(("removedAt", "string"), ("reason", "string")), nextStepId: s13),

            new ActionStepDefinition(s13, "SuspendUser", integrationId, "user.suspend",
                new Dictionary<string, TemplateOrLiteral>
                {
                    ["userId"] = TemplateOrLiteral.Template("{{ContentSubmitted.userId}}")
                },
                FailureStrategy.Stop,
                outputSchema: Schema(("suspendedAt", "string"), ("duration", "string"))),

            new ActionStepDefinition(s14, "HideContent", integrationId, "content.hide",
                new Dictionary<string, TemplateOrLiteral>
                {
                    ["contentId"] = TemplateOrLiteral.Template("{{ContentSubmitted.contentId}}")
                },
                FailureStrategy.Stop,
                outputSchema: Schema(("hiddenAt", "string"), ("reviewRequired", "bool"))),

            new ActionStepDefinition(s15, "LogDecision", integrationId, "audit.logDecision",
                new Dictionary<string, TemplateOrLiteral>
                {
                    ["contentId"] = TemplateOrLiteral.Template("{{ContentSubmitted.contentId}}"),
                    ["overallScore"] = TemplateOrLiteral.Template("{{AggregateScores.overallScore}}")
                },
                FailureStrategy.Skip,
                outputSchema: Schema(("logId", "string"), ("loggedAt", "string")), nextStepId: s16),

            new ActionStepDefinition(s16, "NotifyCreator", integrationId, "notification.send",
                new Dictionary<string, TemplateOrLiteral>
                {
                    ["userId"] = TemplateOrLiteral.Template("{{ContentSubmitted.userId}}"),
                    ["decision"] = TemplateOrLiteral.Template("{{AggregateScores.decision}}")
                },
                FailureStrategy.Skip,
                outputSchema: Schema(("notificationId", "string"), ("sentAt", "string")), nextStepId: s17),

            new ActionStepDefinition(s17, "GenerateReport", integrationId, "report.moderation",
                new Dictionary<string, TemplateOrLiteral>
                {
                    ["contentId"] = TemplateOrLiteral.Template("{{ContentSubmitted.contentId}}")
                },
                FailureStrategy.Skip,
                outputSchema: Schema(("reportId", "string"), ("reportUrl", "string")), nextStepId: s18),

            new ActionStepDefinition(s18, "ArchiveAudit", integrationId, "audit.archive",
                new Dictionary<string, TemplateOrLiteral>
                {
                    ["contentId"] = TemplateOrLiteral.Template("{{ContentSubmitted.contentId}}")
                },
                FailureStrategy.Stop,
                outputSchema: Schema(("archiveId", "string"), ("archivedAt", "string")))
        };

        var result = new WorkflowDefinitionAggregate(NewVersionId(), NewWorkflowId(), steps);

        Assert.NotNull(result);
    }
}
