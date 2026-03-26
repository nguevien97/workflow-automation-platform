// using WorkflowAutomation.SharedKernel.Domain.Enums;
// using WorkflowAutomation.SharedKernel.Domain.Ids;
// using WorkflowAutomation.WorkflowDefinition.Domain.Enums;
// using WorkflowAutomation.WorkflowDefinition.Domain.Ids;
// using WorkflowAutomation.WorkflowDefinition.Domain.StepDefinitions;
// using WorkflowAutomation.WorkflowDefinition.Domain.ValueObjects;
// using WorkflowDefinitionAggregate = WorkflowAutomation.WorkflowDefinition.Domain.Aggregates.WorkflowDefinition;

// namespace WorkflowAutomation.WorkflowDefinition.Domain.Tests;

// /// <summary>
// /// 12 validation failure tests for the E-commerce order processing pipeline (Scenario 1).
// /// Each test uses the full valid 20-step pipeline with exactly one deliberate mutation
// /// to trigger a specific validation error.
// /// Tests are derived from requirements §8.5 and §8.6, not from implementation.
// /// </summary>
// public class WorkflowDefinitionValidationFailureTests
// {
//     private static StepId NewStepId() => new(Guid.NewGuid());
//     private static IntegrationId NewIntegrationId() => new(Guid.NewGuid());
//     private static WorkflowVersionId NewVersionId() => new(Guid.NewGuid());
//     private static WorkflowId NewWorkflowId() => new(Guid.NewGuid());

//     private static StepOutputSchema Schema(params (string key, string type)[] fields)
//     {
//         var dict = new Dictionary<string, string>();
//         foreach (var (key, type) in fields)
//             dict[key] = type;
//         return new StepOutputSchema(dict);
//     }

//     private record EcommerceIds(
//         IntegrationId IntegrationId,
//         StepId S1, StepId S2, StepId S3, StepId S4, StepId S5,
//         StepId S6, StepId S7, StepId S8, StepId S9, StepId S10,
//         StepId S11, StepId S12, StepId S13, StepId S14, StepId S15,
//         StepId S16, StepId S17, StepId S18, StepId S19, StepId S20);

//     /// <summary>
//     /// Builds the valid E-commerce order processing pipeline (20 steps) identical to creation test 1.
//     /// Returns the mutable step list and all step IDs for targeted mutations.
//     /// </summary>
//     private static (List<StepDefinition> steps, EcommerceIds ids) BuildValidEcommercePipeline()
//     {
//         var integrationId = NewIntegrationId();
//         var s1 = NewStepId();
//         var s2 = NewStepId();
//         var s3 = NewStepId();
//         var s4 = NewStepId();
//         var s5 = NewStepId();
//         var s6 = NewStepId();
//         var s7 = NewStepId();
//         var s8 = NewStepId();
//         var s9 = NewStepId();
//         var s10 = NewStepId();
//         var s11 = NewStepId();
//         var s12 = NewStepId();
//         var s13 = NewStepId();
//         var s14 = NewStepId();
//         var s15 = NewStepId();
//         var s16 = NewStepId();
//         var s17 = NewStepId();
//         var s18 = NewStepId();
//         var s19 = NewStepId();
//         var s20 = NewStepId();

//         var steps = new List<StepDefinition>
//         {
//             new TriggerStepDefinition(s1, "OrderReceived", integrationId, "order.created",
//                 new Dictionary<string, string> { ["channel"] = "web" }, s2,
//                 Schema(("orderId", "string"), ("amount", "decimal"), ("customerId", "string"), ("items", "array"))),

//             new ActionStepDefinition(s2, "ValidateOrder", integrationId, "order.validate",
//                 new Dictionary<string, TemplateOrLiteral>
//                 {
//                     ["orderId"] = TemplateOrLiteral.Template("{{OrderReceived.orderId}}"),
//                     ["customerId"] = TemplateOrLiteral.Template("{{OrderReceived.customerId}}")
//                 },
//                 FailureStrategy.Retry, retryCount: 3,
//                 outputSchema: Schema(("isValid", "bool"), ("validationMessage", "string")), nextStepId: s3),

//             new ConditionStepDefinition(s3, "CheckOrderAmount",
//                 new List<ConditionRule>
//                 {
//                     new("{{OrderReceived.amount}} > 1000", s4),
//                     new("{{OrderReceived.amount}} <= 1000", s6)
//                 }, nextStepId: s7),

//             new ActionStepDefinition(s4, "FraudCheck", integrationId, "fraud.check",
//                 new Dictionary<string, TemplateOrLiteral>
//                 {
//                     ["orderId"] = TemplateOrLiteral.Template("{{OrderReceived.orderId}}"),
//                     ["amount"] = TemplateOrLiteral.Template("{{OrderReceived.amount}}")
//                 },
//                 FailureStrategy.Stop,
//                 outputSchema: Schema(("riskScore", "int"), ("flagged", "bool")), nextStepId: s5),

//             new ActionStepDefinition(s5, "ManualReview", integrationId, "review.create",
//                 new Dictionary<string, TemplateOrLiteral>
//                 {
//                     ["riskScore"] = TemplateOrLiteral.Template("{{FraudCheck.riskScore}}")
//                 },
//                 FailureStrategy.Stop,
//                 outputSchema: Schema(("approved", "bool"), ("reviewNote", "string"))),

//             new ActionStepDefinition(s6, "AutoApprove", integrationId, "order.approve",
//                 new Dictionary<string, TemplateOrLiteral>
//                 {
//                     ["orderId"] = TemplateOrLiteral.Template("{{OrderReceived.orderId}}")
//                 },
//                 FailureStrategy.Skip,
//                 outputSchema: Schema(("approved", "bool"), ("approvalCode", "string"))),

//             new ActionStepDefinition(s7, "MergeApproval", integrationId, "approval.merge",
//                 new Dictionary<string, TemplateOrLiteral>
//                 {
//                     ["orderId"] = TemplateOrLiteral.Template("{{OrderReceived.orderId}}")
//                 },
//                 FailureStrategy.Stop,
//                 outputSchema: Schema(("finalApproval", "bool"), ("approver", "string")), nextStepId: s8),

//             new ConditionStepDefinition(s8, "CheckStock",
//                 new List<ConditionRule>
//                 {
//                     new("{{ValidateOrder.isValid}} == true", s9),
//                     new("{{ValidateOrder.isValid}} == false", s10)
//                 }, nextStepId: s11),

//             new ActionStepDefinition(s9, "ReserveStock", integrationId, "stock.reserve",
//                 new Dictionary<string, TemplateOrLiteral>
//                 {
//                     ["items"] = TemplateOrLiteral.Template("{{OrderReceived.items}}")
//                 },
//                 FailureStrategy.Retry, retryCount: 2,
//                 outputSchema: Schema(("reserved", "bool"), ("warehouse", "string"))),

//             new ActionStepDefinition(s10, "BackorderStock", integrationId, "stock.backorder",
//                 new Dictionary<string, TemplateOrLiteral>
//                 {
//                     ["items"] = TemplateOrLiteral.Template("{{OrderReceived.items}}")
//                 },
//                 FailureStrategy.Skip,
//                 outputSchema: Schema(("backorderId", "string"), ("estimatedDate", "string"))),

//             new ActionStepDefinition(s11, "ProcessPayment", integrationId, "payment.process",
//                 new Dictionary<string, TemplateOrLiteral>
//                 {
//                     ["amount"] = TemplateOrLiteral.Template("{{OrderReceived.amount}}"),
//                     ["customerId"] = TemplateOrLiteral.Template("{{OrderReceived.customerId}}")
//                 },
//                 FailureStrategy.Retry, retryCount: 3,
//                 outputSchema: Schema(("transactionId", "string"), ("status", "string")), nextStepId: s12),

//             new ActionStepDefinition(s12, "ConfirmOrder", integrationId, "order.confirm",
//                 new Dictionary<string, TemplateOrLiteral>
//                 {
//                     ["transactionId"] = TemplateOrLiteral.Template("{{ProcessPayment.transactionId}}")
//                 },
//                 FailureStrategy.Stop,
//                 outputSchema: Schema(("confirmationCode", "string"), ("confirmedAt", "string")), nextStepId: s13),

//             new ParallelStepDefinition(s13, "ShipAndNotify",
//                 new List<StepId> { s14, s15 }, nextStepId: s16),

//             new ActionStepDefinition(s14, "ShipOrder", integrationId, "shipping.create",
//                 new Dictionary<string, TemplateOrLiteral>
//                 {
//                     ["orderId"] = TemplateOrLiteral.Template("{{OrderReceived.orderId}}"),
//                     ["confirmationCode"] = TemplateOrLiteral.Template("{{ConfirmOrder.confirmationCode}}")
//                 },
//                 FailureStrategy.Retry, retryCount: 2,
//                 outputSchema: Schema(("trackingNumber", "string"), ("carrier", "string"))),

//             new ActionStepDefinition(s15, "EmailNotification", integrationId, "email.send",
//                 new Dictionary<string, TemplateOrLiteral>
//                 {
//                     ["customerId"] = TemplateOrLiteral.Template("{{OrderReceived.customerId}}"),
//                     ["confirmationCode"] = TemplateOrLiteral.Template("{{ConfirmOrder.confirmationCode}}")
//                 },
//                 FailureStrategy.Skip,
//                 outputSchema: Schema(("emailId", "string"), ("sentAt", "string"))),

//             new ActionStepDefinition(s16, "UpdateDashboard", integrationId, "dashboard.update",
//                 new Dictionary<string, TemplateOrLiteral>
//                 {
//                     ["trackingNumber"] = TemplateOrLiteral.Template("{{ShipOrder.trackingNumber}}")
//                 },
//                 FailureStrategy.Skip,
//                 outputSchema: Schema(("dashboardId", "string"), ("updatedAt", "string")), nextStepId: s17),

//             new ActionStepDefinition(s17, "ArchiveOrder", integrationId, "order.archive",
//                 new Dictionary<string, TemplateOrLiteral>
//                 {
//                     ["orderId"] = TemplateOrLiteral.Template("{{OrderReceived.orderId}}")
//                 },
//                 FailureStrategy.Skip,
//                 outputSchema: Schema(("archiveId", "string"), ("archivedAt", "string")), nextStepId: s18),

//             new ActionStepDefinition(s18, "GenerateInvoice", integrationId, "invoice.generate",
//                 new Dictionary<string, TemplateOrLiteral>
//                 {
//                     ["transactionId"] = TemplateOrLiteral.Template("{{ProcessPayment.transactionId}}"),
//                     ["amount"] = TemplateOrLiteral.Template("{{OrderReceived.amount}}")
//                 },
//                 FailureStrategy.Retry, retryCount: 1,
//                 outputSchema: Schema(("invoiceId", "string"), ("invoiceUrl", "string")), nextStepId: s19),

//             new ActionStepDefinition(s19, "SendInvoice", integrationId, "invoice.send",
//                 new Dictionary<string, TemplateOrLiteral>
//                 {
//                     ["invoiceUrl"] = TemplateOrLiteral.Template("{{GenerateInvoice.invoiceUrl}}"),
//                     ["customerId"] = TemplateOrLiteral.Template("{{OrderReceived.customerId}}")
//                 },
//                 FailureStrategy.Skip,
//                 outputSchema: Schema(("sentStatus", "string"), ("deliveryId", "string")), nextStepId: s20),

//             new ActionStepDefinition(s20, "CompleteOrder", integrationId, "order.complete",
//                 new Dictionary<string, TemplateOrLiteral>
//                 {
//                     ["orderId"] = TemplateOrLiteral.Template("{{OrderReceived.orderId}}"),
//                     ["archiveId"] = TemplateOrLiteral.Template("{{ArchiveOrder.archiveId}}")
//                 },
//                 FailureStrategy.Stop,
//                 outputSchema: Schema(("completedAt", "string"), ("summary", "string")))
//         };

//         return (steps, new EcommerceIds(integrationId, s1, s2, s3, s4, s5, s6, s7, s8, s9, s10,
//             s11, s12, s13, s14, s15, s16, s17, s18, s19, s20));
//     }

//     // ──────────────────────────────────────────────────────────────────────
//     // Error 1: Trigger step has null OutputSchema
//     // ──────────────────────────────────────────────────────────────────────
//     [Fact]
//     public void Ecommerce_TriggerNullOutputSchema_Fails()
//     {
//         var (steps, ids) = BuildValidEcommercePipeline();

//         steps[0] = new TriggerStepDefinition(ids.S1, "OrderReceived", ids.IntegrationId, "order.created",
//             new Dictionary<string, string> { ["channel"] = "web" }, ids.S2,
//             outputSchema: null);

//         var ex = Assert.Throws<InvalidOperationException>(() =>
//             new WorkflowDefinitionAggregate(NewVersionId(), NewWorkflowId(), steps));
//         Assert.Contains("must have a non-null OutputSchema", ex.Message);
//     }

//     // ──────────────────────────────────────────────────────────────────────
//     // Error 2: Trigger is not the first step in the list
//     // ──────────────────────────────────────────────────────────────────────
//     [Fact]
//     public void Ecommerce_TriggerNotFirstStep_Fails()
//     {
//         var (steps, ids) = BuildValidEcommercePipeline();

//         // Swap trigger (index 0) with ValidateOrder action (index 1)
//         (steps[0], steps[1]) = (steps[1], steps[0]);

//         var ex = Assert.Throws<InvalidOperationException>(() =>
//             new WorkflowDefinitionAggregate(NewVersionId(), NewWorkflowId(), steps));
//         Assert.Contains("first step in a workflow definition must be a trigger step", ex.Message);
//     }

//     // ──────────────────────────────────────────────────────────────────────
//     // Error 3: Multiple trigger steps in the workflow
//     // ──────────────────────────────────────────────────────────────────────
//     [Fact]
//     public void Ecommerce_MultipleTriggerSteps_Fails()
//     {
//         var (steps, ids) = BuildValidEcommercePipeline();

//         // Add a second trigger step at the end
//         steps.Add(new TriggerStepDefinition(NewStepId(), "DuplicateTrigger", ids.IntegrationId, "order.updated",
//             new Dictionary<string, string> { ["channel"] = "api" }, ids.S2,
//             Schema(("orderId", "string"), ("status", "string"))));

//         var ex = Assert.Throws<InvalidOperationException>(() =>
//             new WorkflowDefinitionAggregate(NewVersionId(), NewWorkflowId(), steps));
//         Assert.Contains("exactly one trigger step", ex.Message);
//     }

//     // ──────────────────────────────────────────────────────────────────────
//     // Error 4: Action step has null OutputSchema
//     // ──────────────────────────────────────────────────────────────────────
//     [Fact]
//     public void Ecommerce_ActionNullOutputSchema_Fails()
//     {
//         var (steps, ids) = BuildValidEcommercePipeline();

//         // Replace ValidateOrder (index 1) with null OutputSchema
//         steps[1] = new ActionStepDefinition(ids.S2, "ValidateOrder", ids.IntegrationId, "order.validate",
//             new Dictionary<string, TemplateOrLiteral>
//             {
//                 ["orderId"] = TemplateOrLiteral.Template("{{OrderReceived.orderId}}"),
//                 ["customerId"] = TemplateOrLiteral.Template("{{OrderReceived.customerId}}")
//             },
//             FailureStrategy.Retry, retryCount: 3,
//             outputSchema: null, nextStepId: ids.S3);

//         var ex = Assert.Throws<InvalidOperationException>(() =>
//             new WorkflowDefinitionAggregate(NewVersionId(), NewWorkflowId(), steps));
//         Assert.Contains("must have a non-null OutputSchema", ex.Message);
//     }

//     // ──────────────────────────────────────────────────────────────────────
//     // Error 5: NextStepId references a step that does not exist in the list
//     // ──────────────────────────────────────────────────────────────────────
//     [Fact]
//     public void Ecommerce_NextStepIdReferencesNonExistentStep_Fails()
//     {
//         var (steps, ids) = BuildValidEcommercePipeline();
//         var phantomStepId = NewStepId();

//         // Replace MergeApproval (index 6) to point to a phantom step
//         steps[6] = new ActionStepDefinition(ids.S7, "MergeApproval", ids.IntegrationId, "approval.merge",
//             new Dictionary<string, TemplateOrLiteral>
//             {
//                 ["orderId"] = TemplateOrLiteral.Template("{{OrderReceived.orderId}}")
//             },
//             FailureStrategy.Stop,
//             outputSchema: Schema(("finalApproval", "bool"), ("approver", "string")), nextStepId: phantomStepId);

//         var ex = Assert.Throws<InvalidOperationException>(() =>
//             new WorkflowDefinitionAggregate(NewVersionId(), NewWorkflowId(), steps));
//         Assert.Contains("is referenced but does not exist in the workflow", ex.Message);
//     }

//     // ──────────────────────────────────────────────────────────────────────
//     // Error 6: Cycle detected — linear chain back-edge (§8.6 rule 2)
//     //          ArchiveOrder chains back to ValidateOrder, creating a cycle
//     //          in the main linear chain (not through a condition branch)
//     // ──────────────────────────────────────────────────────────────────────
//     [Fact]
//     public void Ecommerce_CycleDetected_Fails()
//     {
//         var (steps, ids) = BuildValidEcommercePipeline();

//         // Replace CompleteOrder (index 19, last step) to point back to ValidateOrder (s2),
//         // creating a linear cycle after all steps are visited: ... → s20 → s2
//         steps[19] = new ActionStepDefinition(ids.S20, "CompleteOrder", ids.IntegrationId, "order.complete",
//             new Dictionary<string, TemplateOrLiteral>
//             {
//                 ["orderId"] = TemplateOrLiteral.Template("{{OrderReceived.orderId}}"),
//                 ["archiveId"] = TemplateOrLiteral.Template("{{ArchiveOrder.archiveId}}")
//             },
//             FailureStrategy.Stop,
//             outputSchema: Schema(("completedAt", "string"), ("summary", "string")), nextStepId: ids.S2);

//         var ex = Assert.Throws<InvalidOperationException>(() =>
//             new WorkflowDefinitionAggregate(NewVersionId(), NewWorkflowId(), steps));
//         Assert.Contains("Cycle detected", ex.Message);
//     }

//     // ──────────────────────────────────────────────────────────────────────
//     // Error 7: Template references an unknown step name (typo)
//     // ──────────────────────────────────────────────────────────────────────
//     [Fact]
//     public void Ecommerce_TemplateReferencesUnknownStepName_Fails()
//     {
//         var (steps, ids) = BuildValidEcommercePipeline();

//         // Replace CompleteOrder (index 19): "ArchivedOrder" instead of "ArchiveOrder"
//         steps[19] = new ActionStepDefinition(ids.S20, "CompleteOrder", ids.IntegrationId, "order.complete",
//             new Dictionary<string, TemplateOrLiteral>
//             {
//                 ["orderId"] = TemplateOrLiteral.Template("{{OrderReceived.orderId}}"),
//                 ["archiveId"] = TemplateOrLiteral.Template("{{ArchivedOrder.archiveId}}")
//             },
//             FailureStrategy.Stop,
//             outputSchema: Schema(("completedAt", "string"), ("summary", "string")));

//         var ex = Assert.Throws<InvalidOperationException>(() =>
//             new WorkflowDefinitionAggregate(NewVersionId(), NewWorkflowId(), steps));
//         Assert.Contains("references unknown step 'ArchivedOrder'", ex.Message);
//     }

//     // ──────────────────────────────────────────────────────────────────────
//     // Error 8: Template references a step that hasn't completed yet (forward reference)
//     // ──────────────────────────────────────────────────────────────────────
//     [Fact]
//     public void Ecommerce_TemplateForwardReference_Fails()
//     {
//         var (steps, ids) = BuildValidEcommercePipeline();

//         // Replace ValidateOrder (index 1) to reference ProcessPayment which is far downstream
//         steps[1] = new ActionStepDefinition(ids.S2, "ValidateOrder", ids.IntegrationId, "order.validate",
//             new Dictionary<string, TemplateOrLiteral>
//             {
//                 ["orderId"] = TemplateOrLiteral.Template("{{OrderReceived.orderId}}"),
//                 ["customerId"] = TemplateOrLiteral.Template("{{OrderReceived.customerId}}"),
//                 ["transactionId"] = TemplateOrLiteral.Template("{{ProcessPayment.transactionId}}")
//             },
//             FailureStrategy.Retry, retryCount: 3,
//             outputSchema: Schema(("isValid", "bool"), ("validationMessage", "string")), nextStepId: ids.S3);

//         var ex = Assert.Throws<InvalidOperationException>(() =>
//             new WorkflowDefinitionAggregate(NewVersionId(), NewWorkflowId(), steps));
//         Assert.Contains("references step 'ProcessPayment' before it is guaranteed to complete", ex.Message);
//     }

//     // ──────────────────────────────────────────────────────────────────────
//     // Error 9: Template references a field that doesn't exist on the step's OutputSchema
//     // ──────────────────────────────────────────────────────────────────────
//     [Fact]
//     public void Ecommerce_TemplateReferencesNonExistentField_Fails()
//     {
//         var (steps, ids) = BuildValidEcommercePipeline();

//         // Replace UpdateDashboard (index 15): "trackingCode" instead of "trackingNumber"
//         steps[15] = new ActionStepDefinition(ids.S16, "UpdateDashboard", ids.IntegrationId, "dashboard.update",
//             new Dictionary<string, TemplateOrLiteral>
//             {
//                 ["trackingNumber"] = TemplateOrLiteral.Template("{{ShipOrder.trackingCode}}")
//             },
//             FailureStrategy.Skip,
//             outputSchema: Schema(("dashboardId", "string"), ("updatedAt", "string")), nextStepId: ids.S17);

//         var ex = Assert.Throws<InvalidOperationException>(() =>
//             new WorkflowDefinitionAggregate(NewVersionId(), NewWorkflowId(), steps));
//         Assert.Contains("references non-existent field 'trackingCode' on step 'ShipOrder'", ex.Message);
//     }

//     // ──────────────────────────────────────────────────────────────────────
//     // Error 10: Orphaned unreachable steps in the workflow
//     // ──────────────────────────────────────────────────────────────────────
//     [Fact]
//     public void Ecommerce_OrphanedUnreachableSteps_Fails()
//     {
//         var (steps, ids) = BuildValidEcommercePipeline();

//         // Add an extra step that no other step references
//         steps.Add(new ActionStepDefinition(NewStepId(), "DanglingStep", ids.IntegrationId, "dangling.action",
//             new Dictionary<string, TemplateOrLiteral>
//             {
//                 ["input"] = TemplateOrLiteral.Literal("data")
//             },
//             FailureStrategy.Stop,
//             outputSchema: Schema(("result", "string"), ("status", "string"))));

//         var ex = Assert.Throws<InvalidOperationException>(() =>
//             new WorkflowDefinitionAggregate(NewVersionId(), NewWorkflowId(), steps));
//         Assert.Contains("unreachable (orphaned) steps", ex.Message);
//     }

//     // ──────────────────────────────────────────────────────────────────────
//     // Error 11: Condition branch last step has NextStepId != null (§8.6 rule 6)
//     //           "The last step in every condition branch and parallel branch
//     //            must have NextStepId = null"
//     // ──────────────────────────────────────────────────────────────────────
//     [Fact]
//     public void Ecommerce_BranchLastStepNextStepIdNotNull_Fails()
//     {
//         var (steps, ids) = BuildValidEcommercePipeline();

//         // AutoApprove (s6, index 5) is the last step of the second condition branch.
//         // It has NextStepId = null. Set it to some step to violate rule 6.
//         steps[5] = new ActionStepDefinition(ids.S6, "AutoApprove", ids.IntegrationId, "order.approve",
//             new Dictionary<string, TemplateOrLiteral>
//             {
//                 ["orderId"] = TemplateOrLiteral.Template("{{OrderReceived.orderId}}")
//             },
//             FailureStrategy.Skip,
//             outputSchema: Schema(("approved", "bool"), ("approvalCode", "string")),
//             nextStepId: ids.S11);

//         var ex = Assert.Throws<InvalidOperationException>(() =>
//             new WorkflowDefinitionAggregate(NewVersionId(), NewWorkflowId(), steps));
//         Assert.Contains("NextStepId", ex.Message);
//     }

//     // ──────────────────────────────────────────────────────────────────────
//     // Error 12: Condition step with zero rules (§8.6 rule 7)
//     //           "A ConditionStepDefinition must have at least one ConditionRule"
//     // ──────────────────────────────────────────────────────────────────────
//     [Fact]
//     public void Ecommerce_ConditionWithZeroRules_Fails()
//     {
//         var (steps, ids) = BuildValidEcommercePipeline();

//         // Replace CheckOrderAmount (s3, index 2) with empty rules list
//         steps[2] = new ConditionStepDefinition(ids.S3, "CheckOrderAmount",
//             new List<ConditionRule>(), nextStepId: ids.S7);

//         var ex = Assert.Throws<InvalidOperationException>(() =>
//             new WorkflowDefinitionAggregate(NewVersionId(), NewWorkflowId(), steps));
//         Assert.Contains("rule", ex.Message, StringComparison.OrdinalIgnoreCase);
//     }
// }

// /// <summary>
// /// 12 validation failure tests for the CI/CD deployment pipeline (Scenario 2).
// /// Each test uses the full valid 20-step pipeline with exactly one deliberate mutation
// /// to trigger a specific validation error.
// /// Tests are derived from requirements §8.5 and §8.6, not from implementation.
// /// </summary>
// public class CiCdValidationFailureTests
// {
//     private static StepId NewStepId() => new(Guid.NewGuid());
//     private static IntegrationId NewIntegrationId() => new(Guid.NewGuid());
//     private static WorkflowVersionId NewVersionId() => new(Guid.NewGuid());
//     private static WorkflowId NewWorkflowId() => new(Guid.NewGuid());

//     private static StepOutputSchema Schema(params (string key, string type)[] fields)
//     {
//         var dict = new Dictionary<string, string>();
//         foreach (var (key, type) in fields)
//             dict[key] = type;
//         return new StepOutputSchema(dict);
//     }

//     private record CiCdIds(
//         IntegrationId IntegrationId,
//         StepId S1, StepId S2, StepId S3, StepId S4, StepId S5,
//         StepId S6, StepId S7, StepId S8, StepId S9, StepId S10,
//         StepId S11, StepId S12, StepId S13, StepId S14, StepId S15,
//         StepId S16, StepId S17, StepId S18, StepId S19, StepId S20);

//     private static (List<StepDefinition> steps, CiCdIds ids) BuildValidCiCdPipeline()
//     {
//         var integrationId = NewIntegrationId();
//         var s1 = NewStepId();
//         var s2 = NewStepId();
//         var s3 = NewStepId();
//         var s4 = NewStepId();
//         var s5 = NewStepId();
//         var s6 = NewStepId();
//         var s7 = NewStepId();
//         var s8 = NewStepId();
//         var s9 = NewStepId();
//         var s10 = NewStepId();
//         var s11 = NewStepId();
//         var s12 = NewStepId();
//         var s13 = NewStepId();
//         var s14 = NewStepId();
//         var s15 = NewStepId();
//         var s16 = NewStepId();
//         var s17 = NewStepId();
//         var s18 = NewStepId();
//         var s19 = NewStepId();
//         var s20 = NewStepId();

//         var steps = new List<StepDefinition>
//         {
//             new TriggerStepDefinition(s1, "PushEvent", integrationId, "git.push",
//                 new Dictionary<string, string> { ["branch"] = "main" }, s2,
//                 Schema(("commitHash", "string"), ("branch", "string"), ("author", "string"), ("repoUrl", "string"))),

//             new ActionStepDefinition(s2, "CloneRepo", integrationId, "git.clone",
//                 new Dictionary<string, TemplateOrLiteral>
//                 {
//                     ["repoUrl"] = TemplateOrLiteral.Template("{{PushEvent.repoUrl}}"),
//                     ["commitHash"] = TemplateOrLiteral.Template("{{PushEvent.commitHash}}")
//                 },
//                 FailureStrategy.Retry, retryCount: 2,
//                 outputSchema: Schema(("workDir", "string"), ("cloneStatus", "string")), nextStepId: s3),

//             new ParallelStepDefinition(s3, "RunTests",
//                 new List<StepId> { s4, s5, s6 }, nextStepId: s7),

//             new ActionStepDefinition(s4, "UnitTests", integrationId, "test.run",
//                 new Dictionary<string, TemplateOrLiteral>
//                 {
//                     ["workDir"] = TemplateOrLiteral.Template("{{CloneRepo.workDir}}"),
//                     ["suite"] = TemplateOrLiteral.Literal("unit")
//                 },
//                 FailureStrategy.Stop,
//                 outputSchema: Schema(("passed", "bool"), ("coverage", "decimal"))),

//             new ActionStepDefinition(s5, "IntegrationTests", integrationId, "test.run",
//                 new Dictionary<string, TemplateOrLiteral>
//                 {
//                     ["workDir"] = TemplateOrLiteral.Template("{{CloneRepo.workDir}}"),
//                     ["suite"] = TemplateOrLiteral.Literal("integration")
//                 },
//                 FailureStrategy.Stop,
//                 outputSchema: Schema(("passed", "bool"), ("failCount", "int"))),

//             new ActionStepDefinition(s6, "LintCheck", integrationId, "lint.run",
//                 new Dictionary<string, TemplateOrLiteral>
//                 {
//                     ["workDir"] = TemplateOrLiteral.Template("{{CloneRepo.workDir}}")
//                 },
//                 FailureStrategy.Skip,
//                 outputSchema: Schema(("passed", "bool"), ("warnings", "int"))),

//             new ConditionStepDefinition(s7, "AllTestsPassed",
//                 new List<ConditionRule>
//                 {
//                     new("{{UnitTests.passed}} == true && {{IntegrationTests.passed}} == true", s8),
//                     new("{{UnitTests.passed}} == false || {{IntegrationTests.passed}} == false", s15)
//                 }, nextStepId: s17),

//             new ActionStepDefinition(s8, "BuildArtifact", integrationId, "build.docker",
//                 new Dictionary<string, TemplateOrLiteral>
//                 {
//                     ["workDir"] = TemplateOrLiteral.Template("{{CloneRepo.workDir}}"),
//                     ["commitHash"] = TemplateOrLiteral.Template("{{PushEvent.commitHash}}")
//                 },
//                 FailureStrategy.Stop,
//                 outputSchema: Schema(("imageTag", "string"), ("buildId", "string")), nextStepId: s9),

//             new ActionStepDefinition(s9, "PushDockerImage", integrationId, "docker.push",
//                 new Dictionary<string, TemplateOrLiteral>
//                 {
//                     ["imageTag"] = TemplateOrLiteral.Template("{{BuildArtifact.imageTag}}")
//                 },
//                 FailureStrategy.Retry, retryCount: 3,
//                 outputSchema: Schema(("registryUrl", "string"), ("digest", "string")), nextStepId: s10),

//             new ConditionStepDefinition(s10, "SelectEnvironment",
//                 new List<ConditionRule>
//                 {
//                     new("{{PushEvent.branch}} == 'staging'", s11),
//                     new("{{PushEvent.branch}} == 'main'", s12)
//                 }, nextStepId: s13),

//             new ActionStepDefinition(s11, "DeployStaging", integrationId, "k8s.deploy",
//                 new Dictionary<string, TemplateOrLiteral>
//                 {
//                     ["imageTag"] = TemplateOrLiteral.Template("{{BuildArtifact.imageTag}}"),
//                     ["env"] = TemplateOrLiteral.Literal("staging")
//                 },
//                 FailureStrategy.Retry, retryCount: 1,
//                 outputSchema: Schema(("deploymentId", "string"), ("status", "string"))),

//             new ActionStepDefinition(s12, "DeployProduction", integrationId, "k8s.deploy",
//                 new Dictionary<string, TemplateOrLiteral>
//                 {
//                     ["imageTag"] = TemplateOrLiteral.Template("{{BuildArtifact.imageTag}}"),
//                     ["env"] = TemplateOrLiteral.Literal("production")
//                 },
//                 FailureStrategy.Stop,
//                 outputSchema: Schema(("deploymentId", "string"), ("status", "string"))),

//             new ActionStepDefinition(s13, "SmokeTest", integrationId, "test.smoke",
//                 new Dictionary<string, TemplateOrLiteral>
//                 {
//                     ["commitHash"] = TemplateOrLiteral.Template("{{PushEvent.commitHash}}")
//                 },
//                 FailureStrategy.Stop,
//                 outputSchema: Schema(("passed", "bool"), ("responseTime", "int")), nextStepId: s14),

//             new ActionStepDefinition(s14, "NotifySuccess", integrationId, "slack.send",
//                 new Dictionary<string, TemplateOrLiteral>
//                 {
//                     ["message"] = TemplateOrLiteral.Template("{{PushEvent.commitHash}}"),
//                     ["channel"] = TemplateOrLiteral.Literal("#deployments")
//                 },
//                 FailureStrategy.Skip,
//                 outputSchema: Schema(("messageId", "string"), ("sentAt", "string"))),

//             new ActionStepDefinition(s15, "Rollback", integrationId, "k8s.rollback",
//                 new Dictionary<string, TemplateOrLiteral>
//                 {
//                     ["commitHash"] = TemplateOrLiteral.Template("{{PushEvent.commitHash}}")
//                 },
//                 FailureStrategy.Stop,
//                 outputSchema: Schema(("rollbackId", "string"), ("status", "string")), nextStepId: s16),

//             new ActionStepDefinition(s16, "AlertTeam", integrationId, "pagerduty.alert",
//                 new Dictionary<string, TemplateOrLiteral>
//                 {
//                     ["author"] = TemplateOrLiteral.Template("{{PushEvent.author}}")
//                 },
//                 FailureStrategy.Skip,
//                 outputSchema: Schema(("alertId", "string"), ("severity", "string"))),

//             new ActionStepDefinition(s17, "UpdateJira", integrationId, "jira.update",
//                 new Dictionary<string, TemplateOrLiteral>
//                 {
//                     ["commitHash"] = TemplateOrLiteral.Template("{{PushEvent.commitHash}}")
//                 },
//                 FailureStrategy.Skip,
//                 outputSchema: Schema(("ticketId", "string"), ("status", "string")), nextStepId: s18),

//             new ActionStepDefinition(s18, "CleanArtifacts", integrationId, "storage.clean",
//                 new Dictionary<string, TemplateOrLiteral>
//                 {
//                     ["workDir"] = TemplateOrLiteral.Template("{{CloneRepo.workDir}}")
//                 },
//                 FailureStrategy.Skip,
//                 outputSchema: Schema(("cleaned", "bool"), ("freedSpace", "string")), nextStepId: s19),

//             new ActionStepDefinition(s19, "LogMetrics", integrationId, "metrics.log",
//                 new Dictionary<string, TemplateOrLiteral>
//                 {
//                     ["commitHash"] = TemplateOrLiteral.Template("{{PushEvent.commitHash}}"),
//                     ["branch"] = TemplateOrLiteral.Template("{{PushEvent.branch}}")
//                 },
//                 FailureStrategy.Skip,
//                 outputSchema: Schema(("logId", "string"), ("timestamp", "string")), nextStepId: s20),

//             new ActionStepDefinition(s20, "FinalizePipeline", integrationId, "pipeline.finalize",
//                 new Dictionary<string, TemplateOrLiteral>
//                 {
//                     ["commitHash"] = TemplateOrLiteral.Template("{{PushEvent.commitHash}}")
//                 },
//                 FailureStrategy.Stop,
//                 outputSchema: Schema(("pipelineId", "string"), ("duration", "string")))
//         };

//         return (steps, new CiCdIds(integrationId, s1, s2, s3, s4, s5, s6, s7, s8, s9, s10,
//             s11, s12, s13, s14, s15, s16, s17, s18, s19, s20));
//     }

//     // ──────────────────────────────────────────────────────────────────────
//     // Error 1: Trigger step has null OutputSchema
//     // ──────────────────────────────────────────────────────────────────────
//     [Fact]
//     public void CiCd_TriggerNullOutputSchema_Fails()
//     {
//         var (steps, ids) = BuildValidCiCdPipeline();

//         steps[0] = new TriggerStepDefinition(ids.S1, "PushEvent", ids.IntegrationId, "git.push",
//             new Dictionary<string, string> { ["branch"] = "main" }, ids.S2,
//             outputSchema: null);

//         var ex = Assert.Throws<InvalidOperationException>(() =>
//             new WorkflowDefinitionAggregate(NewVersionId(), NewWorkflowId(), steps));
//         Assert.Contains("must have a non-null OutputSchema", ex.Message);
//     }

//     // ──────────────────────────────────────────────────────────────────────
//     // Error 2: Trigger is not the first step in the list
//     // ──────────────────────────────────────────────────────────────────────
//     [Fact]
//     public void CiCd_TriggerNotFirstStep_Fails()
//     {
//         var (steps, ids) = BuildValidCiCdPipeline();

//         // Swap trigger (index 0) with CloneRepo action (index 1)
//         (steps[0], steps[1]) = (steps[1], steps[0]);

//         var ex = Assert.Throws<InvalidOperationException>(() =>
//             new WorkflowDefinitionAggregate(NewVersionId(), NewWorkflowId(), steps));
//         Assert.Contains("first step in a workflow definition must be a trigger step", ex.Message);
//     }

//     // ──────────────────────────────────────────────────────────────────────
//     // Error 3: Multiple trigger steps in the workflow
//     // ──────────────────────────────────────────────────────────────────────
//     [Fact]
//     public void CiCd_MultipleTriggerSteps_Fails()
//     {
//         var (steps, ids) = BuildValidCiCdPipeline();

//         steps.Add(new TriggerStepDefinition(NewStepId(), "MergeEvent", ids.IntegrationId, "git.merge",
//             new Dictionary<string, string> { ["branch"] = "develop" }, ids.S2,
//             Schema(("mergeHash", "string"), ("targetBranch", "string"))));

//         var ex = Assert.Throws<InvalidOperationException>(() =>
//             new WorkflowDefinitionAggregate(NewVersionId(), NewWorkflowId(), steps));
//         Assert.Contains("exactly one trigger step", ex.Message);
//     }

//     // ──────────────────────────────────────────────────────────────────────
//     // Error 4: Action step has null OutputSchema
//     // ──────────────────────────────────────────────────────────────────────
//     [Fact]
//     public void CiCd_ActionNullOutputSchema_Fails()
//     {
//         var (steps, ids) = BuildValidCiCdPipeline();

//         // Replace CloneRepo (index 1) with null OutputSchema
//         steps[1] = new ActionStepDefinition(ids.S2, "CloneRepo", ids.IntegrationId, "git.clone",
//             new Dictionary<string, TemplateOrLiteral>
//             {
//                 ["repoUrl"] = TemplateOrLiteral.Template("{{PushEvent.repoUrl}}"),
//                 ["commitHash"] = TemplateOrLiteral.Template("{{PushEvent.commitHash}}")
//             },
//             FailureStrategy.Retry, retryCount: 2,
//             outputSchema: null, nextStepId: ids.S3);

//         var ex = Assert.Throws<InvalidOperationException>(() =>
//             new WorkflowDefinitionAggregate(NewVersionId(), NewWorkflowId(), steps));
//         Assert.Contains("must have a non-null OutputSchema", ex.Message);
//     }

//     // ──────────────────────────────────────────────────────────────────────
//     // Error 5: NextStepId references a step that does not exist
//     // ──────────────────────────────────────────────────────────────────────
//     [Fact]
//     public void CiCd_NextStepIdReferencesNonExistentStep_Fails()
//     {
//         var (steps, ids) = BuildValidCiCdPipeline();
//         var phantomStepId = NewStepId();

//         // Replace PushDockerImage (index 8) to point to phantom step
//         steps[8] = new ActionStepDefinition(ids.S9, "PushDockerImage", ids.IntegrationId, "docker.push",
//             new Dictionary<string, TemplateOrLiteral>
//             {
//                 ["imageTag"] = TemplateOrLiteral.Template("{{BuildArtifact.imageTag}}")
//             },
//             FailureStrategy.Retry, retryCount: 3,
//             outputSchema: Schema(("registryUrl", "string"), ("digest", "string")), nextStepId: phantomStepId);

//         var ex = Assert.Throws<InvalidOperationException>(() =>
//             new WorkflowDefinitionAggregate(NewVersionId(), NewWorkflowId(), steps));
//         Assert.Contains("is referenced but does not exist in the workflow", ex.Message);
//     }

//     // ──────────────────────────────────────────────────────────────────────
//     // Error 6: Cycle detected — linear chain back-edge (§8.6 rule 2)
//     //          LogMetrics chains back to CloneRepo in the main linear chain
//     // ──────────────────────────────────────────────────────────────────────
//     [Fact]
//     public void CiCd_CycleDetected_Fails()
//     {
//         var (steps, ids) = BuildValidCiCdPipeline();

//         // Replace FinalizePipeline (index 19, last step) to point back to UpdateJira (s17),
//         // creating a linear cycle after all steps are visited: ... → s20 → s17
//         steps[19] = new ActionStepDefinition(ids.S20, "FinalizePipeline", ids.IntegrationId, "pipeline.finalize",
//             new Dictionary<string, TemplateOrLiteral>
//             {
//                 ["commitHash"] = TemplateOrLiteral.Template("{{PushEvent.commitHash}}")
//             },
//             FailureStrategy.Stop,
//             outputSchema: Schema(("pipelineId", "string"), ("duration", "string")), nextStepId: ids.S17);

//         var ex = Assert.Throws<InvalidOperationException>(() =>
//             new WorkflowDefinitionAggregate(NewVersionId(), NewWorkflowId(), steps));
//         Assert.Contains("Cycle detected", ex.Message);
//     }

//     // ──────────────────────────────────────────────────────────────────────
//     // Error 7: Template references an unknown step name
//     // ──────────────────────────────────────────────────────────────────────
//     [Fact]
//     public void CiCd_TemplateReferencesUnknownStepName_Fails()
//     {
//         var (steps, ids) = BuildValidCiCdPipeline();

//         // Replace CleanArtifacts (index 17): "ClonedRepo" instead of "CloneRepo"
//         steps[17] = new ActionStepDefinition(ids.S18, "CleanArtifacts", ids.IntegrationId, "storage.clean",
//             new Dictionary<string, TemplateOrLiteral>
//             {
//                 ["workDir"] = TemplateOrLiteral.Template("{{ClonedRepo.workDir}}")
//             },
//             FailureStrategy.Skip,
//             outputSchema: Schema(("cleaned", "bool"), ("freedSpace", "string")), nextStepId: ids.S19);

//         var ex = Assert.Throws<InvalidOperationException>(() =>
//             new WorkflowDefinitionAggregate(NewVersionId(), NewWorkflowId(), steps));
//         Assert.Contains("references unknown step 'ClonedRepo'", ex.Message);
//     }

//     // ──────────────────────────────────────────────────────────────────────
//     // Error 8: Template forward reference — CloneRepo references BuildArtifact
//     // ──────────────────────────────────────────────────────────────────────
//     [Fact]
//     public void CiCd_TemplateForwardReference_Fails()
//     {
//         var (steps, ids) = BuildValidCiCdPipeline();

//         // Replace CloneRepo (index 1) to reference BuildArtifact which is downstream
//         steps[1] = new ActionStepDefinition(ids.S2, "CloneRepo", ids.IntegrationId, "git.clone",
//             new Dictionary<string, TemplateOrLiteral>
//             {
//                 ["repoUrl"] = TemplateOrLiteral.Template("{{PushEvent.repoUrl}}"),
//                 ["commitHash"] = TemplateOrLiteral.Template("{{PushEvent.commitHash}}"),
//                 ["imageTag"] = TemplateOrLiteral.Template("{{BuildArtifact.imageTag}}")
//             },
//             FailureStrategy.Retry, retryCount: 2,
//             outputSchema: Schema(("workDir", "string"), ("cloneStatus", "string")), nextStepId: ids.S3);

//         var ex = Assert.Throws<InvalidOperationException>(() =>
//             new WorkflowDefinitionAggregate(NewVersionId(), NewWorkflowId(), steps));
//         Assert.Contains("references step 'BuildArtifact' before it is guaranteed to complete", ex.Message);
//     }

//     // ──────────────────────────────────────────────────────────────────────
//     // Error 9: Template references a non-existent field on OutputSchema
//     // ──────────────────────────────────────────────────────────────────────
//     [Fact]
//     public void CiCd_TemplateReferencesNonExistentField_Fails()
//     {
//         var (steps, ids) = BuildValidCiCdPipeline();

//         // Replace LogMetrics (index 18): "commitId" instead of "commitHash"
//         steps[18] = new ActionStepDefinition(ids.S19, "LogMetrics", ids.IntegrationId, "metrics.log",
//             new Dictionary<string, TemplateOrLiteral>
//             {
//                 ["commitHash"] = TemplateOrLiteral.Template("{{PushEvent.commitId}}"),
//                 ["branch"] = TemplateOrLiteral.Template("{{PushEvent.branch}}")
//             },
//             FailureStrategy.Skip,
//             outputSchema: Schema(("logId", "string"), ("timestamp", "string")), nextStepId: ids.S20);

//         var ex = Assert.Throws<InvalidOperationException>(() =>
//             new WorkflowDefinitionAggregate(NewVersionId(), NewWorkflowId(), steps));
//         Assert.Contains("references non-existent field 'commitId' on step 'PushEvent'", ex.Message);
//     }

//     // ──────────────────────────────────────────────────────────────────────
//     // Error 10: Orphaned unreachable step
//     // ──────────────────────────────────────────────────────────────────────
//     [Fact]
//     public void CiCd_OrphanedUnreachableSteps_Fails()
//     {
//         var (steps, ids) = BuildValidCiCdPipeline();

//         steps.Add(new ActionStepDefinition(NewStepId(), "DanglingStep", ids.IntegrationId, "dangling.action",
//             new Dictionary<string, TemplateOrLiteral>
//             {
//                 ["input"] = TemplateOrLiteral.Literal("data")
//             },
//             FailureStrategy.Stop,
//             outputSchema: Schema(("result", "string"), ("status", "string"))));

//         var ex = Assert.Throws<InvalidOperationException>(() =>
//             new WorkflowDefinitionAggregate(NewVersionId(), NewWorkflowId(), steps));
//         Assert.Contains("unreachable (orphaned) steps", ex.Message);
//     }

//     // ──────────────────────────────────────────────────────────────────────
//     // Error 11: Condition branch last step has NextStepId != null (§8.6 rule 6)
//     // ──────────────────────────────────────────────────────────────────────
//     [Fact]
//     public void CiCd_BranchLastStepNextStepIdNotNull_Fails()
//     {
//         var (steps, ids) = BuildValidCiCdPipeline();

//         // AlertTeam (s16, index 15) is the last step of the second condition branch (AllTestsPassed).
//         // It has NextStepId = null. Set it to point somewhere to violate rule 6.
//         steps[15] = new ActionStepDefinition(ids.S16, "AlertTeam", ids.IntegrationId, "pagerduty.alert",
//             new Dictionary<string, TemplateOrLiteral>
//             {
//                 ["author"] = TemplateOrLiteral.Template("{{PushEvent.author}}")
//             },
//             FailureStrategy.Skip,
//             outputSchema: Schema(("alertId", "string"), ("severity", "string")),
//             nextStepId: ids.S17);

//         var ex = Assert.Throws<InvalidOperationException>(() =>
//             new WorkflowDefinitionAggregate(NewVersionId(), NewWorkflowId(), steps));
//         Assert.Contains("NextStepId", ex.Message);
//     }

//     // ──────────────────────────────────────────────────────────────────────
//     // Error 12: Condition step with zero rules (§8.6 rule 7)
//     // ──────────────────────────────────────────────────────────────────────
//     [Fact]
//     public void CiCd_ConditionWithZeroRules_Fails()
//     {
//         var (steps, ids) = BuildValidCiCdPipeline();

//         // Replace AllTestsPassed (s7, index 6) with empty rules
//         steps[6] = new ConditionStepDefinition(ids.S7, "AllTestsPassed",
//             new List<ConditionRule>(), nextStepId: ids.S17);

//         var ex = Assert.Throws<InvalidOperationException>(() =>
//             new WorkflowDefinitionAggregate(NewVersionId(), NewWorkflowId(), steps));
//         Assert.Contains("rule", ex.Message, StringComparison.OrdinalIgnoreCase);
//     }
// }

// /// <summary>
// /// 12 validation failure tests for the Customer onboarding pipeline (Scenario 3).
// /// Full 18-step pipeline with loop, parallel, conditions, and template references.
// /// Each test applies one mutation to trigger a specific validation error.
// /// </summary>
// public class CustomerOnboardingValidationFailureTests
// {
//     private static StepId NewStepId() => new(Guid.NewGuid());
//     private static IntegrationId NewIntegrationId() => new(Guid.NewGuid());
//     private static WorkflowVersionId NewVersionId() => new(Guid.NewGuid());
//     private static WorkflowId NewWorkflowId() => new(Guid.NewGuid());

//     private static StepOutputSchema Schema(params (string key, string type)[] fields)
//     {
//         var dict = new Dictionary<string, string>();
//         foreach (var (key, type) in fields)
//             dict[key] = type;
//         return new StepOutputSchema(dict);
//     }

//     private record OnboardingIds(
//         IntegrationId IntegrationId,
//         StepId S1, StepId S2, StepId S3, StepId S3Inner1, StepId S3Inner2,
//         StepId S4, StepId S5, StepId S6, StepId S7, StepId S8,
//         StepId S9, StepId S10, StepId S11, StepId S12, StepId S13,
//         StepId S14, StepId S15, StepId S16, StepId S17, StepId S18);

//     private static (List<StepDefinition> steps, OnboardingIds ids) BuildValidOnboardingPipeline()
//     {
//         var integrationId = NewIntegrationId();
//         var s1 = NewStepId();
//         var s2 = NewStepId();
//         var s3 = NewStepId();
//         var s3Inner1 = NewStepId();
//         var s3Inner2 = NewStepId();
//         var s4 = NewStepId();
//         var s5 = NewStepId();
//         var s6 = NewStepId();
//         var s7 = NewStepId();
//         var s8 = NewStepId();
//         var s9 = NewStepId();
//         var s10 = NewStepId();
//         var s11 = NewStepId();
//         var s12 = NewStepId();
//         var s13 = NewStepId();
//         var s14 = NewStepId();
//         var s15 = NewStepId();
//         var s16 = NewStepId();
//         var s17 = NewStepId();
//         var s18 = NewStepId();

//         var loopInnerSteps = new List<StepDefinition>
//         {
//             new ActionStepDefinition(s3Inner1, "ScanDocument", integrationId, "doc.scan",
//                 new Dictionary<string, TemplateOrLiteral>
//                 {
//                     ["docId"] = TemplateOrLiteral.Literal("currentItem")
//                 },
//                 FailureStrategy.Retry, retryCount: 2,
//                 outputSchema: Schema(("scanResult", "string"), ("confidence", "decimal")), nextStepId: s3Inner2),

//             new ActionStepDefinition(s3Inner2, "ClassifyDocument", integrationId, "doc.classify",
//                 new Dictionary<string, TemplateOrLiteral>
//                 {
//                     ["scanResult"] = TemplateOrLiteral.Literal("scannedData")
//                 },
//                 FailureStrategy.Skip,
//                 outputSchema: Schema(("docType", "string"), ("valid", "bool")))
//         };

//         var steps = new List<StepDefinition>
//         {
//             new TriggerStepDefinition(s1, "NewCustomerEvent", integrationId, "customer.created",
//                 new Dictionary<string, string> { ["source"] = "web-portal" }, s2,
//                 Schema(("customerId", "string"), ("email", "string"), ("documents", "array"), ("tier", "string"))),

//             new ActionStepDefinition(s2, "CreateAccount", integrationId, "account.create",
//                 new Dictionary<string, TemplateOrLiteral>
//                 {
//                     ["customerId"] = TemplateOrLiteral.Template("{{NewCustomerEvent.customerId}}"),
//                     ["email"] = TemplateOrLiteral.Template("{{NewCustomerEvent.email}}")
//                 },
//                 FailureStrategy.Stop,
//                 outputSchema: Schema(("accountId", "string"), ("status", "string")), nextStepId: s3),

//             new LoopStepDefinition(s3, "VerifyDocuments",
//                 new TemplateReference("{{NewCustomerEvent.documents}}"),
//                 Schema(("currentItem", "string")),
//                 loopInnerSteps,
//                 ConcurrencyMode.Sequential,
//                 IterationFailureStrategy.Skip,
//                 retryCount: 0, nextStepId: s4),

//             new ActionStepDefinition(s4, "EnrichProfile", integrationId, "profile.enrich",
//                 new Dictionary<string, TemplateOrLiteral>
//                 {
//                     ["accountId"] = TemplateOrLiteral.Template("{{CreateAccount.accountId}}")
//                 },
//                 FailureStrategy.Skip,
//                 outputSchema: Schema(("enriched", "bool"), ("score", "int")), nextStepId: s5),

//             new ParallelStepDefinition(s5, "VerificationChecks",
//                 new List<StepId> { s6, s7, s8 }, nextStepId: s9),

//             new ActionStepDefinition(s6, "CreditCheck", integrationId, "credit.check",
//                 new Dictionary<string, TemplateOrLiteral>
//                 {
//                     ["customerId"] = TemplateOrLiteral.Template("{{NewCustomerEvent.customerId}}")
//                 },
//                 FailureStrategy.Stop,
//                 outputSchema: Schema(("creditScore", "int"), ("approved", "bool"))),

//             new ActionStepDefinition(s7, "IdentityVerify", integrationId, "identity.verify",
//                 new Dictionary<string, TemplateOrLiteral>
//                 {
//                     ["customerId"] = TemplateOrLiteral.Template("{{NewCustomerEvent.customerId}}")
//                 },
//                 FailureStrategy.Stop,
//                 outputSchema: Schema(("verified", "bool"), ("method", "string"))),

//             new ActionStepDefinition(s8, "AddressVerify", integrationId, "address.verify",
//                 new Dictionary<string, TemplateOrLiteral>
//                 {
//                     ["customerId"] = TemplateOrLiteral.Template("{{NewCustomerEvent.customerId}}")
//                 },
//                 FailureStrategy.Stop,
//                 outputSchema: Schema(("verified", "bool"), ("normalizedAddress", "string"))),

//             new ConditionStepDefinition(s9, "AllVerified",
//                 new List<ConditionRule>
//                 {
//                     new("{{CreditCheck.approved}} == true && {{IdentityVerify.verified}} == true", s10),
//                     new("{{CreditCheck.approved}} == false || {{IdentityVerify.verified}} == false", s12)
//                 }, nextStepId: s14),

//             new ActionStepDefinition(s10, "ActivateAccount", integrationId, "account.activate",
//                 new Dictionary<string, TemplateOrLiteral>
//                 {
//                     ["accountId"] = TemplateOrLiteral.Template("{{CreateAccount.accountId}}")
//                 },
//                 FailureStrategy.Stop,
//                 outputSchema: Schema(("activatedAt", "string"), ("status", "string")), nextStepId: s11),

//             new ActionStepDefinition(s11, "WelcomeEmail", integrationId, "email.welcome",
//                 new Dictionary<string, TemplateOrLiteral>
//                 {
//                     ["email"] = TemplateOrLiteral.Template("{{NewCustomerEvent.email}}")
//                 },
//                 FailureStrategy.Skip,
//                 outputSchema: Schema(("emailId", "string"), ("sentAt", "string"))),

//             new ActionStepDefinition(s12, "ManualReview", integrationId, "review.manual",
//                 new Dictionary<string, TemplateOrLiteral>
//                 {
//                     ["customerId"] = TemplateOrLiteral.Template("{{NewCustomerEvent.customerId}}"),
//                     ["creditScore"] = TemplateOrLiteral.Template("{{CreditCheck.creditScore}}")
//                 },
//                 FailureStrategy.Stop,
//                 outputSchema: Schema(("reviewId", "string"), ("outcome", "string")), nextStepId: s13),

//             new ActionStepDefinition(s13, "NotifyCompliance", integrationId, "compliance.notify",
//                 new Dictionary<string, TemplateOrLiteral>
//                 {
//                     ["customerId"] = TemplateOrLiteral.Template("{{NewCustomerEvent.customerId}}")
//                 },
//                 FailureStrategy.Skip,
//                 outputSchema: Schema(("notificationId", "string"), ("sentAt", "string"))),

//             new ActionStepDefinition(s14, "SyncCRM", integrationId, "crm.sync",
//                 new Dictionary<string, TemplateOrLiteral>
//                 {
//                     ["accountId"] = TemplateOrLiteral.Template("{{CreateAccount.accountId}}")
//                 },
//                 FailureStrategy.Skip,
//                 outputSchema: Schema(("crmId", "string"), ("syncedAt", "string")), nextStepId: s15),

//             new ActionStepDefinition(s15, "AssignManager", integrationId, "manager.assign",
//                 new Dictionary<string, TemplateOrLiteral>
//                 {
//                     ["tier"] = TemplateOrLiteral.Template("{{NewCustomerEvent.tier}}")
//                 },
//                 FailureStrategy.Skip,
//                 outputSchema: Schema(("managerId", "string"), ("managerName", "string")), nextStepId: s16),

//             new ActionStepDefinition(s16, "ScheduleOnboarding", integrationId, "calendar.schedule",
//                 new Dictionary<string, TemplateOrLiteral>
//                 {
//                     ["email"] = TemplateOrLiteral.Template("{{NewCustomerEvent.email}}")
//                 },
//                 FailureStrategy.Skip,
//                 outputSchema: Schema(("meetingId", "string"), ("scheduledAt", "string")), nextStepId: s17),

//             new ActionStepDefinition(s17, "AuditLog", integrationId, "audit.log",
//                 new Dictionary<string, TemplateOrLiteral>
//                 {
//                     ["customerId"] = TemplateOrLiteral.Template("{{NewCustomerEvent.customerId}}"),
//                     ["accountId"] = TemplateOrLiteral.Template("{{CreateAccount.accountId}}")
//                 },
//                 FailureStrategy.Skip,
//                 outputSchema: Schema(("logId", "string"), ("timestamp", "string")), nextStepId: s18),

//             new ActionStepDefinition(s18, "CompleteOnboarding", integrationId, "onboarding.complete",
//                 new Dictionary<string, TemplateOrLiteral>
//                 {
//                     ["customerId"] = TemplateOrLiteral.Template("{{NewCustomerEvent.customerId}}")
//                 },
//                 FailureStrategy.Stop,
//                 outputSchema: Schema(("completedAt", "string"), ("summary", "string")))
//         };

//         return (steps, new OnboardingIds(integrationId, s1, s2, s3, s3Inner1, s3Inner2,
//             s4, s5, s6, s7, s8, s9, s10, s11, s12, s13, s14, s15, s16, s17, s18));
//     }

//     // ──────────────────────────────────────────────────────────────────────
//     // Error 1: Trigger step has null OutputSchema
//     // ──────────────────────────────────────────────────────────────────────
//     [Fact]
//     public void Onboarding_TriggerNullOutputSchema_Fails()
//     {
//         var (steps, ids) = BuildValidOnboardingPipeline();

//         steps[0] = new TriggerStepDefinition(ids.S1, "NewCustomerEvent", ids.IntegrationId, "customer.created",
//             new Dictionary<string, string> { ["source"] = "web-portal" }, ids.S2,
//             outputSchema: null);

//         var ex = Assert.Throws<InvalidOperationException>(() =>
//             new WorkflowDefinitionAggregate(NewVersionId(), NewWorkflowId(), steps));
//         Assert.Contains("must have a non-null OutputSchema", ex.Message);
//     }

//     // ──────────────────────────────────────────────────────────────────────
//     // Error 2: Trigger is not the first step
//     // ──────────────────────────────────────────────────────────────────────
//     [Fact]
//     public void Onboarding_TriggerNotFirstStep_Fails()
//     {
//         var (steps, ids) = BuildValidOnboardingPipeline();

//         (steps[0], steps[1]) = (steps[1], steps[0]);

//         var ex = Assert.Throws<InvalidOperationException>(() =>
//             new WorkflowDefinitionAggregate(NewVersionId(), NewWorkflowId(), steps));
//         Assert.Contains("first step in a workflow definition must be a trigger step", ex.Message);
//     }

//     // ──────────────────────────────────────────────────────────────────────
//     // Error 3: Multiple trigger steps
//     // ──────────────────────────────────────────────────────────────────────
//     [Fact]
//     public void Onboarding_MultipleTriggerSteps_Fails()
//     {
//         var (steps, ids) = BuildValidOnboardingPipeline();

//         steps.Add(new TriggerStepDefinition(NewStepId(), "CustomerUpdated", ids.IntegrationId, "customer.updated",
//             new Dictionary<string, string> { ["source"] = "api" }, ids.S2,
//             Schema(("customerId", "string"), ("updateType", "string"))));

//         var ex = Assert.Throws<InvalidOperationException>(() =>
//             new WorkflowDefinitionAggregate(NewVersionId(), NewWorkflowId(), steps));
//         Assert.Contains("exactly one trigger step", ex.Message);
//     }

//     // ──────────────────────────────────────────────────────────────────────
//     // Error 4: Action step has null OutputSchema
//     // ──────────────────────────────────────────────────────────────────────
//     [Fact]
//     public void Onboarding_ActionNullOutputSchema_Fails()
//     {
//         var (steps, ids) = BuildValidOnboardingPipeline();

//         // Replace CreateAccount (index 1) with null OutputSchema
//         steps[1] = new ActionStepDefinition(ids.S2, "CreateAccount", ids.IntegrationId, "account.create",
//             new Dictionary<string, TemplateOrLiteral>
//             {
//                 ["customerId"] = TemplateOrLiteral.Template("{{NewCustomerEvent.customerId}}"),
//                 ["email"] = TemplateOrLiteral.Template("{{NewCustomerEvent.email}}")
//             },
//             FailureStrategy.Stop,
//             outputSchema: null, nextStepId: ids.S3);

//         var ex = Assert.Throws<InvalidOperationException>(() =>
//             new WorkflowDefinitionAggregate(NewVersionId(), NewWorkflowId(), steps));
//         Assert.Contains("must have a non-null OutputSchema", ex.Message);
//     }

//     // ──────────────────────────────────────────────────────────────────────
//     // Error 5: NextStepId references a step not in the list
//     // ──────────────────────────────────────────────────────────────────────
//     [Fact]
//     public void Onboarding_NextStepIdReferencesNonExistentStep_Fails()
//     {
//         var (steps, ids) = BuildValidOnboardingPipeline();
//         var phantomStepId = NewStepId();

//         // Replace EnrichProfile (index 3) to point to a phantom step
//         steps[3] = new ActionStepDefinition(ids.S4, "EnrichProfile", ids.IntegrationId, "profile.enrich",
//             new Dictionary<string, TemplateOrLiteral>
//             {
//                 ["accountId"] = TemplateOrLiteral.Template("{{CreateAccount.accountId}}")
//             },
//             FailureStrategy.Skip,
//             outputSchema: Schema(("enriched", "bool"), ("score", "int")), nextStepId: phantomStepId);

//         var ex = Assert.Throws<InvalidOperationException>(() =>
//             new WorkflowDefinitionAggregate(NewVersionId(), NewWorkflowId(), steps));
//         Assert.Contains("is referenced but does not exist in the workflow", ex.Message);
//     }

//     // ──────────────────────────────────────────────────────────────────────
//     // Error 6: Cycle detected — linear chain back-edge (§8.6 rule 2)
//     //          AuditLog chains back to CreateAccount in the main linear chain
//     // ──────────────────────────────────────────────────────────────────────
//     [Fact]
//     public void Onboarding_CycleDetected_Fails()
//     {
//         var (steps, ids) = BuildValidOnboardingPipeline();

//         // Replace CompleteOnboarding (index 17, last step) to point back to CreateAccount (s2),
//         // creating a linear cycle after all steps are visited: ... → s18 → s2
//         steps[17] = new ActionStepDefinition(ids.S18, "CompleteOnboarding", ids.IntegrationId, "onboarding.complete",
//             new Dictionary<string, TemplateOrLiteral>
//             {
//                 ["customerId"] = TemplateOrLiteral.Template("{{NewCustomerEvent.customerId}}")
//             },
//             FailureStrategy.Stop,
//             outputSchema: Schema(("completedAt", "string"), ("summary", "string")), nextStepId: ids.S2);

//         var ex = Assert.Throws<InvalidOperationException>(() =>
//             new WorkflowDefinitionAggregate(NewVersionId(), NewWorkflowId(), steps));
//         Assert.Contains("Cycle detected", ex.Message);
//     }

//     // ──────────────────────────────────────────────────────────────────────
//     // Error 7: Template references an unknown step name
//     // ──────────────────────────────────────────────────────────────────────
//     [Fact]
//     public void Onboarding_TemplateReferencesUnknownStepName_Fails()
//     {
//         var (steps, ids) = BuildValidOnboardingPipeline();

//         // Replace SyncCRM (index 13): "AccountCreated" instead of "CreateAccount"
//         steps[13] = new ActionStepDefinition(ids.S14, "SyncCRM", ids.IntegrationId, "crm.sync",
//             new Dictionary<string, TemplateOrLiteral>
//             {
//                 ["accountId"] = TemplateOrLiteral.Template("{{AccountCreated.accountId}}")
//             },
//             FailureStrategy.Skip,
//             outputSchema: Schema(("crmId", "string"), ("syncedAt", "string")), nextStepId: ids.S15);

//         var ex = Assert.Throws<InvalidOperationException>(() =>
//             new WorkflowDefinitionAggregate(NewVersionId(), NewWorkflowId(), steps));
//         Assert.Contains("references unknown step 'AccountCreated'", ex.Message);
//     }

//     // ──────────────────────────────────────────────────────────────────────
//     // Error 8: Template forward reference — CreateAccount references SyncCRM
//     // ──────────────────────────────────────────────────────────────────────
//     [Fact]
//     public void Onboarding_TemplateForwardReference_Fails()
//     {
//         var (steps, ids) = BuildValidOnboardingPipeline();

//         // Replace CreateAccount (index 1) to reference SyncCRM which is downstream
//         steps[1] = new ActionStepDefinition(ids.S2, "CreateAccount", ids.IntegrationId, "account.create",
//             new Dictionary<string, TemplateOrLiteral>
//             {
//                 ["customerId"] = TemplateOrLiteral.Template("{{NewCustomerEvent.customerId}}"),
//                 ["email"] = TemplateOrLiteral.Template("{{NewCustomerEvent.email}}"),
//                 ["crmId"] = TemplateOrLiteral.Template("{{SyncCRM.crmId}}")
//             },
//             FailureStrategy.Stop,
//             outputSchema: Schema(("accountId", "string"), ("status", "string")), nextStepId: ids.S3);

//         var ex = Assert.Throws<InvalidOperationException>(() =>
//             new WorkflowDefinitionAggregate(NewVersionId(), NewWorkflowId(), steps));
//         Assert.Contains("references step 'SyncCRM' before it is guaranteed to complete", ex.Message);
//     }

//     // ──────────────────────────────────────────────────────────────────────
//     // Error 9: Template references a non-existent field
//     // ──────────────────────────────────────────────────────────────────────
//     [Fact]
//     public void Onboarding_TemplateReferencesNonExistentField_Fails()
//     {
//         var (steps, ids) = BuildValidOnboardingPipeline();

//         // Replace AssignManager (index 14): "level" instead of "tier"
//         steps[14] = new ActionStepDefinition(ids.S15, "AssignManager", ids.IntegrationId, "manager.assign",
//             new Dictionary<string, TemplateOrLiteral>
//             {
//                 ["tier"] = TemplateOrLiteral.Template("{{NewCustomerEvent.level}}")
//             },
//             FailureStrategy.Skip,
//             outputSchema: Schema(("managerId", "string"), ("managerName", "string")), nextStepId: ids.S16);

//         var ex = Assert.Throws<InvalidOperationException>(() =>
//             new WorkflowDefinitionAggregate(NewVersionId(), NewWorkflowId(), steps));
//         Assert.Contains("references non-existent field 'level' on step 'NewCustomerEvent'", ex.Message);
//     }

//     // ──────────────────────────────────────────────────────────────────────
//     // Error 10: Orphaned unreachable steps — add 3 dangling steps to exceed
//     //           the loop inner step count inflation in reachable tracking
//     // ──────────────────────────────────────────────────────────────────────
//     [Fact]
//     public void Onboarding_OrphanedUnreachableSteps_Fails()
//     {
//         var (steps, ids) = BuildValidOnboardingPipeline();

//         // Per requirement §8.6 rule 4: all steps must be reachable.
//         // Adding one unreachable step should be detected, regardless of loop inner steps.
//         steps.Add(new ActionStepDefinition(NewStepId(), "DanglingStep", ids.IntegrationId, "dangling.action",
//             new Dictionary<string, TemplateOrLiteral> { ["input"] = TemplateOrLiteral.Literal("data") },
//             FailureStrategy.Stop,
//             outputSchema: Schema(("result", "string"), ("status", "string"))));

//         var ex = Assert.Throws<InvalidOperationException>(() =>
//             new WorkflowDefinitionAggregate(NewVersionId(), NewWorkflowId(), steps));
//         Assert.Contains("unreachable (orphaned) steps", ex.Message);
//     }

//     // ──────────────────────────────────────────────────────────────────────
//     // Error 11: Condition branch last step has NextStepId != null (§8.6 rule 6)
//     // ──────────────────────────────────────────────────────────────────────
//     [Fact]
//     public void Onboarding_BranchLastStepNextStepIdNotNull_Fails()
//     {
//         var (steps, ids) = BuildValidOnboardingPipeline();

//         // WelcomeEmail (s11, index 10) is the last step of the first condition branch.
//         // It has NextStepId = null. Set it to point somewhere.
//         steps[10] = new ActionStepDefinition(ids.S11, "WelcomeEmail", ids.IntegrationId, "email.welcome",
//             new Dictionary<string, TemplateOrLiteral>
//             {
//                 ["email"] = TemplateOrLiteral.Template("{{NewCustomerEvent.email}}")
//             },
//             FailureStrategy.Skip,
//             outputSchema: Schema(("emailId", "string"), ("sentAt", "string")),
//             nextStepId: ids.S14);

//         var ex = Assert.Throws<InvalidOperationException>(() =>
//             new WorkflowDefinitionAggregate(NewVersionId(), NewWorkflowId(), steps));
//         Assert.Contains("NextStepId", ex.Message);
//     }

//     // ──────────────────────────────────────────────────────────────────────
//     // Error 12: Condition step with zero rules (§8.6 rule 7)
//     // ──────────────────────────────────────────────────────────────────────
//     [Fact]
//     public void Onboarding_ConditionWithZeroRules_Fails()
//     {
//         var (steps, ids) = BuildValidOnboardingPipeline();

//         // Replace AllVerified (s9, index 8) with empty rules
//         steps[8] = new ConditionStepDefinition(ids.S9, "AllVerified",
//             new List<ConditionRule>(), nextStepId: ids.S14);

//         var ex = Assert.Throws<InvalidOperationException>(() =>
//             new WorkflowDefinitionAggregate(NewVersionId(), NewWorkflowId(), steps));
//         Assert.Contains("rule", ex.Message, StringComparison.OrdinalIgnoreCase);
//     }
// }

// /// <summary>
// /// 12 validation failure tests for the Data ETL pipeline (Scenario 4).
// /// Full 16-step pipeline with parallel extraction, loop transformation, conditions, and templates.
// /// Each test applies one mutation to trigger a specific validation error.
// /// </summary>
// public class DataEtlValidationFailureTests
// {
//     private static StepId NewStepId() => new(Guid.NewGuid());
//     private static IntegrationId NewIntegrationId() => new(Guid.NewGuid());
//     private static WorkflowVersionId NewVersionId() => new(Guid.NewGuid());
//     private static WorkflowId NewWorkflowId() => new(Guid.NewGuid());

//     private static StepOutputSchema Schema(params (string key, string type)[] fields)
//     {
//         var dict = new Dictionary<string, string>();
//         foreach (var (key, type) in fields)
//             dict[key] = type;
//         return new StepOutputSchema(dict);
//     }

//     private record EtlIds(
//         IntegrationId IntegrationId,
//         StepId S1, StepId S2, StepId S3, StepId S4, StepId S5,
//         StepId S6, StepId S7, StepId S7Inner1, StepId S7Inner2,
//         StepId S8, StepId S9, StepId S10, StepId S11,
//         StepId S12, StepId S13, StepId S14, StepId S15, StepId S16);

//     private static (List<StepDefinition> steps, EtlIds ids) BuildValidEtlPipeline()
//     {
//         var integrationId = NewIntegrationId();
//         var s1 = NewStepId();
//         var s2 = NewStepId();
//         var s3 = NewStepId();
//         var s4 = NewStepId();
//         var s5 = NewStepId();
//         var s6 = NewStepId();
//         var s7 = NewStepId();
//         var s7Inner1 = NewStepId();
//         var s7Inner2 = NewStepId();
//         var s8 = NewStepId();
//         var s9 = NewStepId();
//         var s10 = NewStepId();
//         var s11 = NewStepId();
//         var s12 = NewStepId();
//         var s13 = NewStepId();
//         var s14 = NewStepId();
//         var s15 = NewStepId();
//         var s16 = NewStepId();

//         var loopInnerSteps = new List<StepDefinition>
//         {
//             new ActionStepDefinition(s7Inner1, "NormalizeRecord", integrationId, "data.normalize",
//                 new Dictionary<string, TemplateOrLiteral>
//                 {
//                     ["record"] = TemplateOrLiteral.Literal("currentRecord")
//                 },
//                 FailureStrategy.Skip,
//                 outputSchema: Schema(("normalized", "string"), ("transformType", "string")), nextStepId: s7Inner2),

//             new ActionStepDefinition(s7Inner2, "EnrichRecord", integrationId, "data.enrich",
//                 new Dictionary<string, TemplateOrLiteral>
//                 {
//                     ["normalized"] = TemplateOrLiteral.Literal("normalizedData")
//                 },
//                 FailureStrategy.Skip,
//                 outputSchema: Schema(("enriched", "string"), ("enrichSource", "string")))
//         };

//         var steps = new List<StepDefinition>
//         {
//             new TriggerStepDefinition(s1, "ScheduledETL", integrationId, "cron.trigger",
//                 new Dictionary<string, string> { ["schedule"] = "0 2 * * *" }, s2,
//                 Schema(("runId", "string"), ("timestamp", "string"), ("records", "array"), ("batchSize", "int"))),

//             new ParallelStepDefinition(s2, "ExtractSources",
//                 new List<StepId> { s3, s4, s5 }, nextStepId: s6),

//             new ActionStepDefinition(s3, "ExtractDB", integrationId, "db.query",
//                 new Dictionary<string, TemplateOrLiteral>
//                 {
//                     ["runId"] = TemplateOrLiteral.Template("{{ScheduledETL.runId}}")
//                 },
//                 FailureStrategy.Retry, retryCount: 3,
//                 outputSchema: Schema(("rowCount", "int"), ("dataRef", "string"))),

//             new ActionStepDefinition(s4, "ExtractAPI", integrationId, "api.fetch",
//                 new Dictionary<string, TemplateOrLiteral>
//                 {
//                     ["runId"] = TemplateOrLiteral.Template("{{ScheduledETL.runId}}")
//                 },
//                 FailureStrategy.Retry, retryCount: 2,
//                 outputSchema: Schema(("recordCount", "int"), ("dataRef", "string"))),

//             new ActionStepDefinition(s5, "ExtractCSV", integrationId, "csv.import",
//                 new Dictionary<string, TemplateOrLiteral>
//                 {
//                     ["runId"] = TemplateOrLiteral.Template("{{ScheduledETL.runId}}")
//                 },
//                 FailureStrategy.Skip,
//                 outputSchema: Schema(("lineCount", "int"), ("dataRef", "string"))),

//             new ActionStepDefinition(s6, "MergeData", integrationId, "data.merge",
//                 new Dictionary<string, TemplateOrLiteral>
//                 {
//                     ["dbRef"] = TemplateOrLiteral.Template("{{ExtractDB.dataRef}}"),
//                     ["apiRef"] = TemplateOrLiteral.Template("{{ExtractAPI.dataRef}}"),
//                     ["csvRef"] = TemplateOrLiteral.Template("{{ExtractCSV.dataRef}}")
//                 },
//                 FailureStrategy.Stop,
//                 outputSchema: Schema(("mergedRef", "string"), ("totalRecords", "int")), nextStepId: s7),

//             new LoopStepDefinition(s7, "TransformRecords",
//                 new TemplateReference("{{ScheduledETL.records}}"),
//                 Schema(("currentRecord", "string")),
//                 loopInnerSteps,
//                 ConcurrencyMode.Parallel,
//                 IterationFailureStrategy.Skip,
//                 retryCount: 0, nextStepId: s8, maxConcurrency: 10),

//             new ActionStepDefinition(s8, "ValidateData", integrationId, "data.validate",
//                 new Dictionary<string, TemplateOrLiteral>
//                 {
//                     ["mergedRef"] = TemplateOrLiteral.Template("{{MergeData.mergedRef}}")
//                 },
//                 FailureStrategy.Stop,
//                 outputSchema: Schema(("qualityScore", "decimal"), ("validRecords", "int")), nextStepId: s9),

//             new ConditionStepDefinition(s9, "DataQuality",
//                 new List<ConditionRule>
//                 {
//                     new("{{ValidateData.qualityScore}} >= 0.95", s10),
//                     new("{{ValidateData.qualityScore}} < 0.95", s11)
//                 }, nextStepId: s12),

//             new ActionStepDefinition(s10, "LoadWarehouse", integrationId, "warehouse.load",
//                 new Dictionary<string, TemplateOrLiteral>
//                 {
//                     ["mergedRef"] = TemplateOrLiteral.Template("{{MergeData.mergedRef}}")
//                 },
//                 FailureStrategy.Retry, retryCount: 2,
//                 outputSchema: Schema(("loadedCount", "int"), ("tableRef", "string"))),

//             new ActionStepDefinition(s11, "Quarantine", integrationId, "data.quarantine",
//                 new Dictionary<string, TemplateOrLiteral>
//                 {
//                     ["mergedRef"] = TemplateOrLiteral.Template("{{MergeData.mergedRef}}")
//                 },
//                 FailureStrategy.Stop,
//                 outputSchema: Schema(("quarantinedCount", "int"), ("reason", "string"))),

//             new ActionStepDefinition(s12, "IndexData", integrationId, "search.index",
//                 new Dictionary<string, TemplateOrLiteral>
//                 {
//                     ["runId"] = TemplateOrLiteral.Template("{{ScheduledETL.runId}}")
//                 },
//                 FailureStrategy.Skip,
//                 outputSchema: Schema(("indexedCount", "int"), ("indexName", "string")), nextStepId: s13),

//             new ActionStepDefinition(s13, "NotifyTeam", integrationId, "slack.notify",
//                 new Dictionary<string, TemplateOrLiteral>
//                 {
//                     ["runId"] = TemplateOrLiteral.Template("{{ScheduledETL.runId}}"),
//                     ["totalRecords"] = TemplateOrLiteral.Template("{{MergeData.totalRecords}}")
//                 },
//                 FailureStrategy.Skip,
//                 outputSchema: Schema(("messageId", "string"), ("sentAt", "string")), nextStepId: s14),

//             new ActionStepDefinition(s14, "GenerateReport", integrationId, "report.generate",
//                 new Dictionary<string, TemplateOrLiteral>
//                 {
//                     ["runId"] = TemplateOrLiteral.Template("{{ScheduledETL.runId}}")
//                 },
//                 FailureStrategy.Skip,
//                 outputSchema: Schema(("reportUrl", "string"), ("reportId", "string")), nextStepId: s15),

//             new ActionStepDefinition(s15, "ArchiveRaw", integrationId, "storage.archive",
//                 new Dictionary<string, TemplateOrLiteral>
//                 {
//                     ["runId"] = TemplateOrLiteral.Template("{{ScheduledETL.runId}}")
//                 },
//                 FailureStrategy.Skip,
//                 outputSchema: Schema(("archiveId", "string"), ("size", "string")), nextStepId: s16),

//             new ActionStepDefinition(s16, "UpdateCatalog", integrationId, "catalog.update",
//                 new Dictionary<string, TemplateOrLiteral>
//                 {
//                     ["runId"] = TemplateOrLiteral.Template("{{ScheduledETL.runId}}"),
//                     ["reportUrl"] = TemplateOrLiteral.Template("{{GenerateReport.reportUrl}}")
//                 },
//                 FailureStrategy.Skip,
//                 outputSchema: Schema(("catalogId", "string"), ("updatedAt", "string")))
//         };

//         return (steps, new EtlIds(integrationId, s1, s2, s3, s4, s5, s6, s7, s7Inner1, s7Inner2,
//             s8, s9, s10, s11, s12, s13, s14, s15, s16));
//     }

//     // ──────────────────────────────────────────────────────────────────────
//     // Error 1: Trigger step has null OutputSchema
//     // ──────────────────────────────────────────────────────────────────────
//     [Fact]
//     public void Etl_TriggerNullOutputSchema_Fails()
//     {
//         var (steps, ids) = BuildValidEtlPipeline();

//         steps[0] = new TriggerStepDefinition(ids.S1, "ScheduledETL", ids.IntegrationId, "cron.trigger",
//             new Dictionary<string, string> { ["schedule"] = "0 2 * * *" }, ids.S2,
//             outputSchema: null);

//         var ex = Assert.Throws<InvalidOperationException>(() =>
//             new WorkflowDefinitionAggregate(NewVersionId(), NewWorkflowId(), steps));
//         Assert.Contains("must have a non-null OutputSchema", ex.Message);
//     }

//     // ──────────────────────────────────────────────────────────────────────
//     // Error 2: Trigger is not the first step
//     // ──────────────────────────────────────────────────────────────────────
//     [Fact]
//     public void Etl_TriggerNotFirstStep_Fails()
//     {
//         var (steps, ids) = BuildValidEtlPipeline();

//         // Swap trigger (index 0) with ExtractSources parallel (index 1)
//         (steps[0], steps[1]) = (steps[1], steps[0]);

//         var ex = Assert.Throws<InvalidOperationException>(() =>
//             new WorkflowDefinitionAggregate(NewVersionId(), NewWorkflowId(), steps));
//         Assert.Contains("first step in a workflow definition must be a trigger step", ex.Message);
//     }

//     // ──────────────────────────────────────────────────────────────────────
//     // Error 3: Multiple trigger steps
//     // ──────────────────────────────────────────────────────────────────────
//     [Fact]
//     public void Etl_MultipleTriggerSteps_Fails()
//     {
//         var (steps, ids) = BuildValidEtlPipeline();

//         steps.Add(new TriggerStepDefinition(NewStepId(), "ManualETL", ids.IntegrationId, "manual.trigger",
//             new Dictionary<string, string> { ["mode"] = "adhoc" }, ids.S2,
//             Schema(("runId", "string"), ("timestamp", "string"))));

//         var ex = Assert.Throws<InvalidOperationException>(() =>
//             new WorkflowDefinitionAggregate(NewVersionId(), NewWorkflowId(), steps));
//         Assert.Contains("exactly one trigger step", ex.Message);
//     }

//     // ──────────────────────────────────────────────────────────────────────
//     // Error 4: Action step has null OutputSchema
//     // ──────────────────────────────────────────────────────────────────────
//     [Fact]
//     public void Etl_ActionNullOutputSchema_Fails()
//     {
//         var (steps, ids) = BuildValidEtlPipeline();

//         // Replace MergeData (index 5) with null OutputSchema
//         steps[5] = new ActionStepDefinition(ids.S6, "MergeData", ids.IntegrationId, "data.merge",
//             new Dictionary<string, TemplateOrLiteral>
//             {
//                 ["dbRef"] = TemplateOrLiteral.Template("{{ExtractDB.dataRef}}"),
//                 ["apiRef"] = TemplateOrLiteral.Template("{{ExtractAPI.dataRef}}"),
//                 ["csvRef"] = TemplateOrLiteral.Template("{{ExtractCSV.dataRef}}")
//             },
//             FailureStrategy.Stop,
//             outputSchema: null, nextStepId: ids.S7);

//         var ex = Assert.Throws<InvalidOperationException>(() =>
//             new WorkflowDefinitionAggregate(NewVersionId(), NewWorkflowId(), steps));
//         Assert.Contains("must have a non-null OutputSchema", ex.Message);
//     }

//     // ──────────────────────────────────────────────────────────────────────
//     // Error 5: NextStepId references a step not in the list
//     // ──────────────────────────────────────────────────────────────────────
//     [Fact]
//     public void Etl_NextStepIdReferencesNonExistentStep_Fails()
//     {
//         var (steps, ids) = BuildValidEtlPipeline();
//         var phantomStepId = NewStepId();

//         // Replace MergeData (index 5) to point to phantom step
//         steps[5] = new ActionStepDefinition(ids.S6, "MergeData", ids.IntegrationId, "data.merge",
//             new Dictionary<string, TemplateOrLiteral>
//             {
//                 ["dbRef"] = TemplateOrLiteral.Template("{{ExtractDB.dataRef}}"),
//                 ["apiRef"] = TemplateOrLiteral.Template("{{ExtractAPI.dataRef}}"),
//                 ["csvRef"] = TemplateOrLiteral.Template("{{ExtractCSV.dataRef}}")
//             },
//             FailureStrategy.Stop,
//             outputSchema: Schema(("mergedRef", "string"), ("totalRecords", "int")), nextStepId: phantomStepId);

//         var ex = Assert.Throws<InvalidOperationException>(() =>
//             new WorkflowDefinitionAggregate(NewVersionId(), NewWorkflowId(), steps));
//         Assert.Contains("is referenced but does not exist in the workflow", ex.Message);
//     }

//     // ──────────────────────────────────────────────────────────────────────
//     // Error 6: Cycle detected — linear chain back-edge (§8.6 rule 2)
//     //          ArchiveRaw chains back to MergeData in the main linear chain
//     // ──────────────────────────────────────────────────────────────────────
//     [Fact]
//     public void Etl_CycleDetected_Fails()
//     {
//         var (steps, ids) = BuildValidEtlPipeline();

//         // Replace UpdateCatalog (index 15, last step) to point back to MergeData (s6),
//         // creating a linear cycle after all steps are visited: ... → s16 → s6
//         steps[15] = new ActionStepDefinition(ids.S16, "UpdateCatalog", ids.IntegrationId, "catalog.update",
//             new Dictionary<string, TemplateOrLiteral>
//             {
//                 ["runId"] = TemplateOrLiteral.Template("{{ScheduledETL.runId}}"),
//                 ["reportUrl"] = TemplateOrLiteral.Template("{{GenerateReport.reportUrl}}")
//             },
//             FailureStrategy.Skip,
//             outputSchema: Schema(("catalogId", "string"), ("updatedAt", "string")), nextStepId: ids.S6);

//         var ex = Assert.Throws<InvalidOperationException>(() =>
//             new WorkflowDefinitionAggregate(NewVersionId(), NewWorkflowId(), steps));
//         Assert.Contains("Cycle detected", ex.Message);
//     }

//     // ──────────────────────────────────────────────────────────────────────
//     // Error 7: Template references an unknown step name
//     // ──────────────────────────────────────────────────────────────────────
//     [Fact]
//     public void Etl_TemplateReferencesUnknownStepName_Fails()
//     {
//         var (steps, ids) = BuildValidEtlPipeline();

//         // Replace UpdateCatalog (index 15): "ReportGenerator" instead of "GenerateReport"
//         steps[15] = new ActionStepDefinition(ids.S16, "UpdateCatalog", ids.IntegrationId, "catalog.update",
//             new Dictionary<string, TemplateOrLiteral>
//             {
//                 ["runId"] = TemplateOrLiteral.Template("{{ScheduledETL.runId}}"),
//                 ["reportUrl"] = TemplateOrLiteral.Template("{{ReportGenerator.reportUrl}}")
//             },
//             FailureStrategy.Skip,
//             outputSchema: Schema(("catalogId", "string"), ("updatedAt", "string")));

//         var ex = Assert.Throws<InvalidOperationException>(() =>
//             new WorkflowDefinitionAggregate(NewVersionId(), NewWorkflowId(), steps));
//         Assert.Contains("references unknown step 'ReportGenerator'", ex.Message);
//     }

//     // ──────────────────────────────────────────────────────────────────────
//     // Error 8: Template forward reference — ExtractDB references MergeData
//     // ──────────────────────────────────────────────────────────────────────
//     [Fact]
//     public void Etl_TemplateForwardReference_Fails()
//     {
//         var (steps, ids) = BuildValidEtlPipeline();

//         // Replace ExtractDB (index 2) to reference MergeData which is downstream
//         steps[2] = new ActionStepDefinition(ids.S3, "ExtractDB", ids.IntegrationId, "db.query",
//             new Dictionary<string, TemplateOrLiteral>
//             {
//                 ["runId"] = TemplateOrLiteral.Template("{{ScheduledETL.runId}}"),
//                 ["mergedRef"] = TemplateOrLiteral.Template("{{MergeData.mergedRef}}")
//             },
//             FailureStrategy.Retry, retryCount: 3,
//             outputSchema: Schema(("rowCount", "int"), ("dataRef", "string")));

//         var ex = Assert.Throws<InvalidOperationException>(() =>
//             new WorkflowDefinitionAggregate(NewVersionId(), NewWorkflowId(), steps));
//         Assert.Contains("references step 'MergeData' before it is guaranteed to complete", ex.Message);
//     }

//     // ──────────────────────────────────────────────────────────────────────
//     // Error 9: Template references a non-existent field
//     // ──────────────────────────────────────────────────────────────────────
//     [Fact]
//     public void Etl_TemplateReferencesNonExistentField_Fails()
//     {
//         var (steps, ids) = BuildValidEtlPipeline();

//         // Replace NotifyTeam (index 12): "recordCount" instead of "totalRecords"
//         steps[12] = new ActionStepDefinition(ids.S13, "NotifyTeam", ids.IntegrationId, "slack.notify",
//             new Dictionary<string, TemplateOrLiteral>
//             {
//                 ["runId"] = TemplateOrLiteral.Template("{{ScheduledETL.runId}}"),
//                 ["totalRecords"] = TemplateOrLiteral.Template("{{MergeData.recordCount}}")
//             },
//             FailureStrategy.Skip,
//             outputSchema: Schema(("messageId", "string"), ("sentAt", "string")), nextStepId: ids.S14);

//         var ex = Assert.Throws<InvalidOperationException>(() =>
//             new WorkflowDefinitionAggregate(NewVersionId(), NewWorkflowId(), steps));
//         Assert.Contains("references non-existent field 'recordCount' on step 'MergeData'", ex.Message);
//     }

//     // ──────────────────────────────────────────────────────────────────────
//     // Error 10: Orphaned unreachable steps
//     // ──────────────────────────────────────────────────────────────────────
//     [Fact]
//     public void Etl_OrphanedUnreachableSteps_Fails()
//     {
//         var (steps, ids) = BuildValidEtlPipeline();

//         // Per requirement §8.6 rule 4: all steps must be reachable.
//         // Adding one unreachable step should be detected, regardless of loop inner steps.
//         steps.Add(new ActionStepDefinition(NewStepId(), "DanglingStep", ids.IntegrationId, "dangling.action",
//             new Dictionary<string, TemplateOrLiteral> { ["input"] = TemplateOrLiteral.Literal("data") },
//             FailureStrategy.Stop,
//             outputSchema: Schema(("result", "string"), ("status", "string"))));

//         var ex = Assert.Throws<InvalidOperationException>(() =>
//             new WorkflowDefinitionAggregate(NewVersionId(), NewWorkflowId(), steps));
//         Assert.Contains("unreachable (orphaned) steps", ex.Message);
//     }

//     // ──────────────────────────────────────────────────────────────────────
//     // Error 11: Condition branch last step has NextStepId != null (§8.6 rule 6)
//     // ──────────────────────────────────────────────────────────────────────
//     [Fact]
//     public void Etl_BranchLastStepNextStepIdNotNull_Fails()
//     {
//         var (steps, ids) = BuildValidEtlPipeline();

//         // Quarantine (s11, index 10) is the last step of the second condition branch (DataQuality).
//         // It has NextStepId = null. Set it to point somewhere.
//         steps[10] = new ActionStepDefinition(ids.S11, "Quarantine", ids.IntegrationId, "data.quarantine",
//             new Dictionary<string, TemplateOrLiteral>
//             {
//                 ["mergedRef"] = TemplateOrLiteral.Template("{{MergeData.mergedRef}}")
//             },
//             FailureStrategy.Stop,
//             outputSchema: Schema(("quarantinedCount", "int"), ("reason", "string")),
//             nextStepId: ids.S12);

//         var ex = Assert.Throws<InvalidOperationException>(() =>
//             new WorkflowDefinitionAggregate(NewVersionId(), NewWorkflowId(), steps));
//         Assert.Contains("NextStepId", ex.Message);
//     }

//     // ──────────────────────────────────────────────────────────────────────
//     // Error 12: Condition step with zero rules (§8.6 rule 7)
//     // ──────────────────────────────────────────────────────────────────────
//     [Fact]
//     public void Etl_ConditionWithZeroRules_Fails()
//     {
//         var (steps, ids) = BuildValidEtlPipeline();

//         // Replace DataQuality (s9, index 8) with empty rules
//         steps[8] = new ConditionStepDefinition(ids.S9, "DataQuality",
//             new List<ConditionRule>(), nextStepId: ids.S12);

//         var ex = Assert.Throws<InvalidOperationException>(() =>
//             new WorkflowDefinitionAggregate(NewVersionId(), NewWorkflowId(), steps));
//         Assert.Contains("rule", ex.Message, StringComparison.OrdinalIgnoreCase);
//     }
// }

// /// <summary>
// /// 12 validation failure tests for the Incident Management pipeline (Scenario 5).
// /// 20-step pipeline with nested conditions (SeverityCheck, Resolved), parallel critical actions,
// /// and extensive template references. No loops — orphan test needs only 1 dangling step.
// /// Tests are derived from requirements §8.5 and §8.6, not from implementation.
// /// </summary>
// public class IncidentMgmtValidationFailureTests
// {
//     private static StepId NewStepId() => new(Guid.NewGuid());
//     private static IntegrationId NewIntegrationId() => new(Guid.NewGuid());
//     private static WorkflowVersionId NewVersionId() => new(Guid.NewGuid());
//     private static WorkflowId NewWorkflowId() => new(Guid.NewGuid());

//     private static StepOutputSchema Schema(params (string key, string type)[] fields)
//     {
//         var dict = new Dictionary<string, string>();
//         foreach (var (key, type) in fields)
//             dict[key] = type;
//         return new StepOutputSchema(dict);
//     }

//     private record IncidentIds(
//         IntegrationId IntegrationId,
//         StepId S1, StepId S2, StepId S3, StepId S4, StepId S5,
//         StepId S6, StepId S7, StepId S8, StepId S9, StepId S10,
//         StepId S11, StepId S12, StepId S13, StepId S14, StepId S15,
//         StepId S16, StepId S17, StepId S18, StepId S19, StepId S20);

//     private static (List<StepDefinition> steps, IncidentIds ids) BuildValidIncidentPipeline()
//     {
//         var integrationId = NewIntegrationId();
//         var s1 = NewStepId();
//         var s2 = NewStepId();
//         var s3 = NewStepId();
//         var s4 = NewStepId();
//         var s5 = NewStepId();
//         var s6 = NewStepId();
//         var s7 = NewStepId();
//         var s8 = NewStepId();
//         var s9 = NewStepId();
//         var s10 = NewStepId();
//         var s11 = NewStepId();
//         var s12 = NewStepId();
//         var s13 = NewStepId();
//         var s14 = NewStepId();
//         var s15 = NewStepId();
//         var s16 = NewStepId();
//         var s17 = NewStepId();
//         var s18 = NewStepId();
//         var s19 = NewStepId();
//         var s20 = NewStepId();

//         var steps = new List<StepDefinition>
//         {
//             new TriggerStepDefinition(s1, "AlertFired", integrationId, "monitoring.alert",
//                 new Dictionary<string, string> { ["source"] = "datadog" }, s2,
//                 Schema(("alertId", "string"), ("severity", "string"), ("service", "string"), ("message", "string"))),

//             new ActionStepDefinition(s2, "ClassifyIncident", integrationId, "incident.classify",
//                 new Dictionary<string, TemplateOrLiteral>
//                 {
//                     ["alertId"] = TemplateOrLiteral.Template("{{AlertFired.alertId}}"),
//                     ["message"] = TemplateOrLiteral.Template("{{AlertFired.message}}")
//                 },
//                 FailureStrategy.Stop,
//                 outputSchema: Schema(("incidentId", "string"), ("category", "string"), ("priority", "int")), nextStepId: s3),

//             new ConditionStepDefinition(s3, "SeverityCheck",
//                 new List<ConditionRule>
//                 {
//                     new("{{AlertFired.severity}} == 'critical'", s4),
//                     new("{{AlertFired.severity}} == 'high'", s8),
//                     new("{{AlertFired.severity}} == 'low'", s9)
//                 }, nextStepId: s10),

//             new ActionStepDefinition(s4, "PageOnCall", integrationId, "pagerduty.page",
//                 new Dictionary<string, TemplateOrLiteral>
//                 {
//                     ["service"] = TemplateOrLiteral.Template("{{AlertFired.service}}"),
//                     ["incidentId"] = TemplateOrLiteral.Template("{{ClassifyIncident.incidentId}}")
//                 },
//                 FailureStrategy.Retry, retryCount: 3,
//                 outputSchema: Schema(("pageId", "string"), ("acknowledged", "bool")), nextStepId: s5),

//             new ParallelStepDefinition(s5, "CriticalActions",
//                 new List<StepId> { s6, s7 }),

//             new ActionStepDefinition(s6, "RestartService", integrationId, "k8s.restart",
//                 new Dictionary<string, TemplateOrLiteral>
//                 {
//                     ["service"] = TemplateOrLiteral.Template("{{AlertFired.service}}")
//                 },
//                 FailureStrategy.Retry, retryCount: 2,
//                 outputSchema: Schema(("restartId", "string"), ("status", "string"))),

//             new ActionStepDefinition(s7, "RollbackDeploy", integrationId, "deploy.rollback",
//                 new Dictionary<string, TemplateOrLiteral>
//                 {
//                     ["service"] = TemplateOrLiteral.Template("{{AlertFired.service}}")
//                 },
//                 FailureStrategy.Stop,
//                 outputSchema: Schema(("rollbackId", "string"), ("version", "string"))),

//             new ActionStepDefinition(s8, "Escalate", integrationId, "incident.escalate",
//                 new Dictionary<string, TemplateOrLiteral>
//                 {
//                     ["incidentId"] = TemplateOrLiteral.Template("{{ClassifyIncident.incidentId}}")
//                 },
//                 FailureStrategy.Stop,
//                 outputSchema: Schema(("escalationId", "string"), ("assignee", "string"))),

//             new ActionStepDefinition(s9, "CreateTicket", integrationId, "jira.create",
//                 new Dictionary<string, TemplateOrLiteral>
//                 {
//                     ["incidentId"] = TemplateOrLiteral.Template("{{ClassifyIncident.incidentId}}"),
//                     ["category"] = TemplateOrLiteral.Template("{{ClassifyIncident.category}}")
//                 },
//                 FailureStrategy.Stop,
//                 outputSchema: Schema(("ticketId", "string"), ("ticketUrl", "string"))),

//             new ActionStepDefinition(s10, "InvestigateRoot", integrationId, "incident.investigate",
//                 new Dictionary<string, TemplateOrLiteral>
//                 {
//                     ["incidentId"] = TemplateOrLiteral.Template("{{ClassifyIncident.incidentId}}")
//                 },
//                 FailureStrategy.Stop,
//                 outputSchema: Schema(("rootCause", "string"), ("resolved", "bool")), nextStepId: s11),

//             new ConditionStepDefinition(s11, "Resolved",
//                 new List<ConditionRule>
//                 {
//                     new("{{InvestigateRoot.resolved}} == true", s12),
//                     new("{{InvestigateRoot.resolved}} == false", s13)
//                 }, nextStepId: s14),

//             new ActionStepDefinition(s12, "CloseTicket", integrationId, "jira.close",
//                 new Dictionary<string, TemplateOrLiteral>
//                 {
//                     ["incidentId"] = TemplateOrLiteral.Template("{{ClassifyIncident.incidentId}}")
//                 },
//                 FailureStrategy.Stop,
//                 outputSchema: Schema(("closedAt", "string"), ("resolution", "string"))),

//             new ActionStepDefinition(s13, "ReEscalate", integrationId, "incident.reescalate",
//                 new Dictionary<string, TemplateOrLiteral>
//                 {
//                     ["incidentId"] = TemplateOrLiteral.Template("{{ClassifyIncident.incidentId}}"),
//                     ["rootCause"] = TemplateOrLiteral.Template("{{InvestigateRoot.rootCause}}")
//                 },
//                 FailureStrategy.Stop,
//                 outputSchema: Schema(("escalationId", "string"), ("newAssignee", "string"))),

//             new ActionStepDefinition(s14, "PostMortem", integrationId, "docs.create",
//                 new Dictionary<string, TemplateOrLiteral>
//                 {
//                     ["incidentId"] = TemplateOrLiteral.Template("{{ClassifyIncident.incidentId}}"),
//                     ["rootCause"] = TemplateOrLiteral.Template("{{InvestigateRoot.rootCause}}")
//                 },
//                 FailureStrategy.Skip,
//                 outputSchema: Schema(("docId", "string"), ("docUrl", "string")), nextStepId: s15),

//             new ActionStepDefinition(s15, "UpdateRunbook", integrationId, "runbook.update",
//                 new Dictionary<string, TemplateOrLiteral>
//                 {
//                     ["docUrl"] = TemplateOrLiteral.Template("{{PostMortem.docUrl}}")
//                 },
//                 FailureStrategy.Skip,
//                 outputSchema: Schema(("runbookId", "string"), ("updatedAt", "string")), nextStepId: s16),

//             new ActionStepDefinition(s16, "LogMetrics", integrationId, "metrics.log",
//                 new Dictionary<string, TemplateOrLiteral>
//                 {
//                     ["incidentId"] = TemplateOrLiteral.Template("{{ClassifyIncident.incidentId}}")
//                 },
//                 FailureStrategy.Skip,
//                 outputSchema: Schema(("metricId", "string"), ("duration", "string")), nextStepId: s17),

//             new ActionStepDefinition(s17, "WeeklyDigest", integrationId, "report.digest",
//                 new Dictionary<string, TemplateOrLiteral>
//                 {
//                     ["incidentId"] = TemplateOrLiteral.Template("{{ClassifyIncident.incidentId}}")
//                 },
//                 FailureStrategy.Skip,
//                 outputSchema: Schema(("digestId", "string"), ("generatedAt", "string")), nextStepId: s18),

//             new ActionStepDefinition(s18, "ArchiveIncident", integrationId, "incident.archive",
//                 new Dictionary<string, TemplateOrLiteral>
//                 {
//                     ["incidentId"] = TemplateOrLiteral.Template("{{ClassifyIncident.incidentId}}")
//                 },
//                 FailureStrategy.Skip,
//                 outputSchema: Schema(("archiveId", "string"), ("archivedAt", "string")), nextStepId: s19),

//             new ActionStepDefinition(s19, "TrainModel", integrationId, "ml.train",
//                 new Dictionary<string, TemplateOrLiteral>
//                 {
//                     ["rootCause"] = TemplateOrLiteral.Template("{{InvestigateRoot.rootCause}}")
//                 },
//                 FailureStrategy.Skip,
//                 outputSchema: Schema(("modelVersion", "string"), ("accuracy", "decimal")), nextStepId: s20),

//             new ActionStepDefinition(s20, "FinalizeIncident", integrationId, "incident.finalize",
//                 new Dictionary<string, TemplateOrLiteral>
//                 {
//                     ["incidentId"] = TemplateOrLiteral.Template("{{ClassifyIncident.incidentId}}")
//                 },
//                 FailureStrategy.Stop,
//                 outputSchema: Schema(("finalizedAt", "string"), ("status", "string")))
//         };

//         return (steps, new IncidentIds(integrationId, s1, s2, s3, s4, s5, s6, s7, s8, s9, s10,
//             s11, s12, s13, s14, s15, s16, s17, s18, s19, s20));
//     }

//     // ──────────────────────────────────────────────────────────────────────
//     // Error 1: Trigger step has null OutputSchema
//     // ──────────────────────────────────────────────────────────────────────
//     [Fact]
//     public void Incident_TriggerNullOutputSchema_Fails()
//     {
//         var (steps, ids) = BuildValidIncidentPipeline();

//         steps[0] = new TriggerStepDefinition(ids.S1, "AlertFired", ids.IntegrationId, "monitoring.alert",
//             new Dictionary<string, string> { ["source"] = "datadog" }, ids.S2,
//             outputSchema: null);

//         var ex = Assert.Throws<InvalidOperationException>(() =>
//             new WorkflowDefinitionAggregate(NewVersionId(), NewWorkflowId(), steps));
//         Assert.Contains("must have a non-null OutputSchema", ex.Message);
//     }

//     // ──────────────────────────────────────────────────────────────────────
//     // Error 2: Trigger is not the first step
//     // ──────────────────────────────────────────────────────────────────────
//     [Fact]
//     public void Incident_TriggerNotFirstStep_Fails()
//     {
//         var (steps, ids) = BuildValidIncidentPipeline();

//         // Swap trigger (index 0) with ClassifyIncident (index 1)
//         (steps[0], steps[1]) = (steps[1], steps[0]);

//         var ex = Assert.Throws<InvalidOperationException>(() =>
//             new WorkflowDefinitionAggregate(NewVersionId(), NewWorkflowId(), steps));
//         Assert.Contains("first step in a workflow definition must be a trigger step", ex.Message);
//     }

//     // ──────────────────────────────────────────────────────────────────────
//     // Error 3: Multiple trigger steps
//     // ──────────────────────────────────────────────────────────────────────
//     [Fact]
//     public void Incident_MultipleTriggerSteps_Fails()
//     {
//         var (steps, ids) = BuildValidIncidentPipeline();

//         steps.Add(new TriggerStepDefinition(NewStepId(), "ManualAlert", ids.IntegrationId, "manual.alert",
//             new Dictionary<string, string> { ["mode"] = "manual" }, ids.S2,
//             Schema(("alertId", "string"), ("severity", "string"))));

//         var ex = Assert.Throws<InvalidOperationException>(() =>
//             new WorkflowDefinitionAggregate(NewVersionId(), NewWorkflowId(), steps));
//         Assert.Contains("exactly one trigger step", ex.Message);
//     }

//     // ──────────────────────────────────────────────────────────────────────
//     // Error 4: Action step has null OutputSchema
//     // ──────────────────────────────────────────────────────────────────────
//     [Fact]
//     public void Incident_ActionNullOutputSchema_Fails()
//     {
//         var (steps, ids) = BuildValidIncidentPipeline();

//         // Replace ClassifyIncident (index 1) with null OutputSchema
//         steps[1] = new ActionStepDefinition(ids.S2, "ClassifyIncident", ids.IntegrationId, "incident.classify",
//             new Dictionary<string, TemplateOrLiteral>
//             {
//                 ["alertId"] = TemplateOrLiteral.Template("{{AlertFired.alertId}}"),
//                 ["message"] = TemplateOrLiteral.Template("{{AlertFired.message}}")
//             },
//             FailureStrategy.Stop,
//             outputSchema: null, nextStepId: ids.S3);

//         var ex = Assert.Throws<InvalidOperationException>(() =>
//             new WorkflowDefinitionAggregate(NewVersionId(), NewWorkflowId(), steps));
//         Assert.Contains("must have a non-null OutputSchema", ex.Message);
//     }

//     // ──────────────────────────────────────────────────────────────────────
//     // Error 5: NextStepId references a step not in the list
//     // ──────────────────────────────────────────────────────────────────────
//     [Fact]
//     public void Incident_NextStepIdReferencesNonExistentStep_Fails()
//     {
//         var (steps, ids) = BuildValidIncidentPipeline();
//         var phantomStepId = NewStepId();

//         // Replace InvestigateRoot (index 9) to point to phantom step
//         steps[9] = new ActionStepDefinition(ids.S10, "InvestigateRoot", ids.IntegrationId, "incident.investigate",
//             new Dictionary<string, TemplateOrLiteral>
//             {
//                 ["incidentId"] = TemplateOrLiteral.Template("{{ClassifyIncident.incidentId}}")
//             },
//             FailureStrategy.Stop,
//             outputSchema: Schema(("rootCause", "string"), ("resolved", "bool")), nextStepId: phantomStepId);

//         var ex = Assert.Throws<InvalidOperationException>(() =>
//             new WorkflowDefinitionAggregate(NewVersionId(), NewWorkflowId(), steps));
//         Assert.Contains("is referenced but does not exist in the workflow", ex.Message);
//     }

//     // ──────────────────────────────────────────────────────────────────────
//     // Error 6: Cycle detected — linear chain back-edge (§8.6 rule 2)
//     //          TrainModel chains back to InvestigateRoot in the main linear chain
//     // ──────────────────────────────────────────────────────────────────────
//     [Fact]
//     public void Incident_CycleDetected_Fails()
//     {
//         var (steps, ids) = BuildValidIncidentPipeline();

//         // Replace FinalizeIncident (index 19, last step) to point back to InvestigateRoot (s10),
//         // creating a linear cycle after all steps are visited: ... → s20 → s10
//         steps[19] = new ActionStepDefinition(ids.S20, "FinalizeIncident", ids.IntegrationId, "incident.finalize",
//             new Dictionary<string, TemplateOrLiteral>
//             {
//                 ["incidentId"] = TemplateOrLiteral.Template("{{ClassifyIncident.incidentId}}")
//             },
//             FailureStrategy.Stop,
//             outputSchema: Schema(("finalizedAt", "string"), ("status", "string")), nextStepId: ids.S10);

//         var ex = Assert.Throws<InvalidOperationException>(() =>
//             new WorkflowDefinitionAggregate(NewVersionId(), NewWorkflowId(), steps));
//         Assert.Contains("Cycle detected", ex.Message);
//     }

//     // ──────────────────────────────────────────────────────────────────────
//     // Error 7: Template references an unknown step name
//     // ──────────────────────────────────────────────────────────────────────
//     [Fact]
//     public void Incident_TemplateReferencesUnknownStepName_Fails()
//     {
//         var (steps, ids) = BuildValidIncidentPipeline();

//         // Replace PostMortem (index 13): "RootCauseAnalysis" instead of "InvestigateRoot"
//         steps[13] = new ActionStepDefinition(ids.S14, "PostMortem", ids.IntegrationId, "docs.create",
//             new Dictionary<string, TemplateOrLiteral>
//             {
//                 ["incidentId"] = TemplateOrLiteral.Template("{{ClassifyIncident.incidentId}}"),
//                 ["rootCause"] = TemplateOrLiteral.Template("{{RootCauseAnalysis.rootCause}}")
//             },
//             FailureStrategy.Skip,
//             outputSchema: Schema(("docId", "string"), ("docUrl", "string")), nextStepId: ids.S15);

//         var ex = Assert.Throws<InvalidOperationException>(() =>
//             new WorkflowDefinitionAggregate(NewVersionId(), NewWorkflowId(), steps));
//         Assert.Contains("references unknown step 'RootCauseAnalysis'", ex.Message);
//     }

//     // ──────────────────────────────────────────────────────────────────────
//     // Error 8: Template forward reference — ClassifyIncident references PostMortem
//     // ──────────────────────────────────────────────────────────────────────
//     [Fact]
//     public void Incident_TemplateForwardReference_Fails()
//     {
//         var (steps, ids) = BuildValidIncidentPipeline();

//         // Replace ClassifyIncident (index 1) to reference PostMortem.docUrl which is downstream
//         steps[1] = new ActionStepDefinition(ids.S2, "ClassifyIncident", ids.IntegrationId, "incident.classify",
//             new Dictionary<string, TemplateOrLiteral>
//             {
//                 ["alertId"] = TemplateOrLiteral.Template("{{AlertFired.alertId}}"),
//                 ["docUrl"] = TemplateOrLiteral.Template("{{PostMortem.docUrl}}")
//             },
//             FailureStrategy.Stop,
//             outputSchema: Schema(("incidentId", "string"), ("category", "string"), ("priority", "int")), nextStepId: ids.S3);

//         var ex = Assert.Throws<InvalidOperationException>(() =>
//             new WorkflowDefinitionAggregate(NewVersionId(), NewWorkflowId(), steps));
//         Assert.Contains("references step 'PostMortem' before it is guaranteed to complete", ex.Message);
//     }

//     // ──────────────────────────────────────────────────────────────────────
//     // Error 9: Template references a non-existent field
//     // ──────────────────────────────────────────────────────────────────────
//     [Fact]
//     public void Incident_TemplateReferencesNonExistentField_Fails()
//     {
//         var (steps, ids) = BuildValidIncidentPipeline();

//         // Replace TrainModel (index 18): "diagnosis" instead of "rootCause"
//         steps[18] = new ActionStepDefinition(ids.S19, "TrainModel", ids.IntegrationId, "ml.train",
//             new Dictionary<string, TemplateOrLiteral>
//             {
//                 ["rootCause"] = TemplateOrLiteral.Template("{{InvestigateRoot.diagnosis}}")
//             },
//             FailureStrategy.Skip,
//             outputSchema: Schema(("modelVersion", "string"), ("accuracy", "decimal")), nextStepId: ids.S20);

//         var ex = Assert.Throws<InvalidOperationException>(() =>
//             new WorkflowDefinitionAggregate(NewVersionId(), NewWorkflowId(), steps));
//         Assert.Contains("references non-existent field 'diagnosis' on step 'InvestigateRoot'", ex.Message);
//     }

//     // ──────────────────────────────────────────────────────────────────────
//     // Error 10: Orphaned unreachable steps
//     // ──────────────────────────────────────────────────────────────────────
//     [Fact]
//     public void Incident_OrphanedUnreachableSteps_Fails()
//     {
//         var (steps, ids) = BuildValidIncidentPipeline();

//         // No loops — 1 dangling step is enough
//         steps.Add(new ActionStepDefinition(NewStepId(), "DanglingStep1", ids.IntegrationId, "dangling.action",
//             new Dictionary<string, TemplateOrLiteral> { ["input"] = TemplateOrLiteral.Literal("data") },
//             FailureStrategy.Stop,
//             outputSchema: Schema(("result", "string"), ("status", "string"))));

//         var ex = Assert.Throws<InvalidOperationException>(() =>
//             new WorkflowDefinitionAggregate(NewVersionId(), NewWorkflowId(), steps));
//         Assert.Contains("unreachable (orphaned) steps", ex.Message);
//     }

//     // ──────────────────────────────────────────────────────────────────────
//     // Error 11: Condition branch last step has NextStepId != null (§8.6 rule 6)
//     // ──────────────────────────────────────────────────────────────────────
//     [Fact]
//     public void Incident_BranchLastStepNextStepIdNotNull_Fails()
//     {
//         var (steps, ids) = BuildValidIncidentPipeline();

//         // Escalate (s8, index 7) is the last step of the "high" severity branch.
//         // It has NextStepId = null. Set it to point somewhere.
//         steps[7] = new ActionStepDefinition(ids.S8, "Escalate", ids.IntegrationId, "incident.escalate",
//             new Dictionary<string, TemplateOrLiteral>
//             {
//                 ["incidentId"] = TemplateOrLiteral.Template("{{ClassifyIncident.incidentId}}")
//             },
//             FailureStrategy.Stop,
//             outputSchema: Schema(("escalationId", "string"), ("assignee", "string")),
//             nextStepId: ids.S10);

//         var ex = Assert.Throws<InvalidOperationException>(() =>
//             new WorkflowDefinitionAggregate(NewVersionId(), NewWorkflowId(), steps));
//         Assert.Contains("NextStepId", ex.Message);
//     }

//     // ──────────────────────────────────────────────────────────────────────
//     // Error 12: Condition step with zero rules (§8.6 rule 7)
//     // ──────────────────────────────────────────────────────────────────────
//     [Fact]
//     public void Incident_ConditionWithZeroRules_Fails()
//     {
//         var (steps, ids) = BuildValidIncidentPipeline();

//         // Replace SeverityCheck (s3, index 2) with empty rules
//         steps[2] = new ConditionStepDefinition(ids.S3, "SeverityCheck",
//             new List<ConditionRule>(), nextStepId: ids.S10);

//         var ex = Assert.Throws<InvalidOperationException>(() =>
//             new WorkflowDefinitionAggregate(NewVersionId(), NewWorkflowId(), steps));
//         Assert.Contains("rule", ex.Message, StringComparison.OrdinalIgnoreCase);
//     }
// }

// /// <summary>
// /// 12 validation failure tests for the Marketing Campaign pipeline (Scenario 6).
// /// 16-step pipeline with loop (ProcessSegments, 2 inner steps), parallel delivery
// /// channels (Email, SMS, Push), condition (PerformanceCheck), and template references.
// /// Tests are derived from requirements §8.5 and §8.6, not from implementation.
// /// </summary>
// public class MarketingCampaignValidationFailureTests
// {
//     private static StepId NewStepId() => new(Guid.NewGuid());
//     private static IntegrationId NewIntegrationId() => new(Guid.NewGuid());
//     private static WorkflowVersionId NewVersionId() => new(Guid.NewGuid());
//     private static WorkflowId NewWorkflowId() => new(Guid.NewGuid());

//     private static StepOutputSchema Schema(params (string key, string type)[] fields)
//     {
//         var dict = new Dictionary<string, string>();
//         foreach (var (key, type) in fields)
//             dict[key] = type;
//         return new StepOutputSchema(dict);
//     }

//     private record MarketingIds(
//         IntegrationId IntegrationId,
//         StepId S1, StepId S2, StepId S3, StepId S3Inner1, StepId S3Inner2,
//         StepId S4, StepId S5, StepId S6, StepId S7, StepId S8,
//         StepId S9, StepId S10, StepId S11, StepId S12,
//         StepId S13, StepId S14, StepId S15, StepId S16);

//     private static (List<StepDefinition> steps, MarketingIds ids) BuildValidMarketingPipeline()
//     {
//         var integrationId = NewIntegrationId();
//         var s1 = NewStepId();
//         var s2 = NewStepId();
//         var s3 = NewStepId();
//         var s3Inner1 = NewStepId();
//         var s3Inner2 = NewStepId();
//         var s4 = NewStepId();
//         var s5 = NewStepId();
//         var s6 = NewStepId();
//         var s7 = NewStepId();
//         var s8 = NewStepId();
//         var s9 = NewStepId();
//         var s10 = NewStepId();
//         var s11 = NewStepId();
//         var s12 = NewStepId();
//         var s13 = NewStepId();
//         var s14 = NewStepId();
//         var s15 = NewStepId();
//         var s16 = NewStepId();

//         var loopInnerSteps = new List<StepDefinition>
//         {
//             new ActionStepDefinition(s3Inner1, "ScoreSegment", integrationId, "ml.score",
//                 new Dictionary<string, TemplateOrLiteral>
//                 {
//                     ["segment"] = TemplateOrLiteral.Literal("currentSegment")
//                 },
//                 FailureStrategy.Skip,
//                 outputSchema: Schema(("score", "decimal"), ("tier", "string")), nextStepId: s3Inner2),

//             new ActionStepDefinition(s3Inner2, "PersonalizeContent", integrationId, "content.personalize",
//                 new Dictionary<string, TemplateOrLiteral>
//                 {
//                     ["tier"] = TemplateOrLiteral.Literal("premium")
//                 },
//                 FailureStrategy.Skip,
//                 outputSchema: Schema(("contentId", "string"), ("variant", "string")))
//         };

//         var steps = new List<StepDefinition>
//         {
//             new TriggerStepDefinition(s1, "CampaignLaunch", integrationId, "campaign.launch",
//                 new Dictionary<string, string> { ["platform"] = "marketing-hub" }, s2,
//                 Schema(("campaignId", "string"), ("budget", "decimal"), ("segments", "array"), ("startDate", "string"))),

//             new ActionStepDefinition(s2, "LoadAudience", integrationId, "audience.load",
//                 new Dictionary<string, TemplateOrLiteral>
//                 {
//                     ["campaignId"] = TemplateOrLiteral.Template("{{CampaignLaunch.campaignId}}")
//                 },
//                 FailureStrategy.Stop,
//                 outputSchema: Schema(("audienceSize", "int"), ("segmentCount", "int")), nextStepId: s3),

//             new LoopStepDefinition(s3, "ProcessSegments",
//                 new TemplateReference("{{CampaignLaunch.segments}}"),
//                 Schema(("currentSegment", "string")),
//                 loopInnerSteps,
//                 ConcurrencyMode.Parallel,
//                 IterationFailureStrategy.Skip,
//                 retryCount: 0, nextStepId: s4, maxConcurrency: 5),

//             new ActionStepDefinition(s4, "BuildCreatives", integrationId, "creative.build",
//                 new Dictionary<string, TemplateOrLiteral>
//                 {
//                     ["campaignId"] = TemplateOrLiteral.Template("{{CampaignLaunch.campaignId}}")
//                 },
//                 FailureStrategy.Retry, retryCount: 1,
//                 outputSchema: Schema(("creativeIds", "array"), ("count", "int")), nextStepId: s5),

//             new ParallelStepDefinition(s5, "DeliverChannels",
//                 new List<StepId> { s6, s7, s8 }, nextStepId: s9),

//             new ActionStepDefinition(s6, "EmailChannel", integrationId, "email.blast",
//                 new Dictionary<string, TemplateOrLiteral>
//                 {
//                     ["campaignId"] = TemplateOrLiteral.Template("{{CampaignLaunch.campaignId}}"),
//                     ["audienceSize"] = TemplateOrLiteral.Template("{{LoadAudience.audienceSize}}")
//                 },
//                 FailureStrategy.Skip,
//                 outputSchema: Schema(("sent", "int"), ("opened", "int"), ("bounced", "int"))),

//             new ActionStepDefinition(s7, "SMSChannel", integrationId, "sms.blast",
//                 new Dictionary<string, TemplateOrLiteral>
//                 {
//                     ["campaignId"] = TemplateOrLiteral.Template("{{CampaignLaunch.campaignId}}")
//                 },
//                 FailureStrategy.Skip,
//                 outputSchema: Schema(("sent", "int"), ("delivered", "int"))),

//             new ActionStepDefinition(s8, "PushChannel", integrationId, "push.blast",
//                 new Dictionary<string, TemplateOrLiteral>
//                 {
//                     ["campaignId"] = TemplateOrLiteral.Template("{{CampaignLaunch.campaignId}}")
//                 },
//                 FailureStrategy.Skip,
//                 outputSchema: Schema(("sent", "int"), ("clicked", "int"))),

//             new ActionStepDefinition(s9, "MergeDeliveryResults", integrationId, "delivery.merge",
//                 new Dictionary<string, TemplateOrLiteral>
//                 {
//                     ["emailSent"] = TemplateOrLiteral.Template("{{EmailChannel.sent}}"),
//                     ["smsSent"] = TemplateOrLiteral.Template("{{SMSChannel.sent}}"),
//                     ["pushSent"] = TemplateOrLiteral.Template("{{PushChannel.sent}}")
//                 },
//                 FailureStrategy.Stop,
//                 outputSchema: Schema(("totalSent", "int"), ("conversionRate", "decimal")), nextStepId: s10),

//             new ConditionStepDefinition(s10, "PerformanceCheck",
//                 new List<ConditionRule>
//                 {
//                     new("{{MergeDeliveryResults.conversionRate}} >= 0.05", s11),
//                     new("{{MergeDeliveryResults.conversionRate}} < 0.05", s12)
//                 }, nextStepId: s13),

//             new ActionStepDefinition(s11, "ScaleBudget", integrationId, "budget.scale",
//                 new Dictionary<string, TemplateOrLiteral>
//                 {
//                     ["budget"] = TemplateOrLiteral.Template("{{CampaignLaunch.budget}}")
//                 },
//                 FailureStrategy.Stop,
//                 outputSchema: Schema(("newBudget", "decimal"), ("scaleFactor", "decimal"))),

//             new ActionStepDefinition(s12, "PauseCampaign", integrationId, "campaign.pause",
//                 new Dictionary<string, TemplateOrLiteral>
//                 {
//                     ["campaignId"] = TemplateOrLiteral.Template("{{CampaignLaunch.campaignId}}")
//                 },
//                 FailureStrategy.Stop,
//                 outputSchema: Schema(("pausedAt", "string"), ("reason", "string"))),

//             new ActionStepDefinition(s13, "UpdateAnalytics", integrationId, "analytics.update",
//                 new Dictionary<string, TemplateOrLiteral>
//                 {
//                     ["campaignId"] = TemplateOrLiteral.Template("{{CampaignLaunch.campaignId}}"),
//                     ["totalSent"] = TemplateOrLiteral.Template("{{MergeDeliveryResults.totalSent}}")
//                 },
//                 FailureStrategy.Skip,
//                 outputSchema: Schema(("analyticsId", "string"), ("updatedAt", "string")), nextStepId: s14),

//             new ActionStepDefinition(s14, "ReportROI", integrationId, "report.roi",
//                 new Dictionary<string, TemplateOrLiteral>
//                 {
//                     ["campaignId"] = TemplateOrLiteral.Template("{{CampaignLaunch.campaignId}}")
//                 },
//                 FailureStrategy.Skip,
//                 outputSchema: Schema(("roi", "decimal"), ("reportUrl", "string")), nextStepId: s15),

//             new ActionStepDefinition(s15, "ArchiveCampaign", integrationId, "campaign.archive",
//                 new Dictionary<string, TemplateOrLiteral>
//                 {
//                     ["campaignId"] = TemplateOrLiteral.Template("{{CampaignLaunch.campaignId}}")
//                 },
//                 FailureStrategy.Skip,
//                 outputSchema: Schema(("archiveId", "string"), ("archivedAt", "string")), nextStepId: s16),

//             new ActionStepDefinition(s16, "ScheduleNext", integrationId, "campaign.schedule",
//                 new Dictionary<string, TemplateOrLiteral>
//                 {
//                     ["campaignId"] = TemplateOrLiteral.Template("{{CampaignLaunch.campaignId}}"),
//                     ["startDate"] = TemplateOrLiteral.Template("{{CampaignLaunch.startDate}}")
//                 },
//                 FailureStrategy.Skip,
//                 outputSchema: Schema(("nextCampaignId", "string"), ("scheduledAt", "string")))
//         };

//         return (steps, new MarketingIds(integrationId, s1, s2, s3, s3Inner1, s3Inner2,
//             s4, s5, s6, s7, s8, s9, s10, s11, s12, s13, s14, s15, s16));
//     }

//     // ──────────────────────────────────────────────────────────────────────
//     // Error 1: Trigger step has null OutputSchema (§8.6 rule 10)
//     // ──────────────────────────────────────────────────────────────────────
//     [Fact]
//     public void Marketing_TriggerNullOutputSchema_Fails()
//     {
//         var (steps, ids) = BuildValidMarketingPipeline();

//         steps[0] = new TriggerStepDefinition(ids.S1, "CampaignLaunch", ids.IntegrationId, "campaign.launch",
//             new Dictionary<string, string> { ["platform"] = "marketing-hub" }, ids.S2,
//             outputSchema: null);

//         var ex = Assert.Throws<InvalidOperationException>(() =>
//             new WorkflowDefinitionAggregate(NewVersionId(), NewWorkflowId(), steps));
//         Assert.Contains("must have a non-null OutputSchema", ex.Message);
//     }

//     // ──────────────────────────────────────────────────────────────────────
//     // Error 2: Trigger is not the first step (§8.6 rule 1)
//     // ──────────────────────────────────────────────────────────────────────
//     [Fact]
//     public void Marketing_TriggerNotFirstStep_Fails()
//     {
//         var (steps, ids) = BuildValidMarketingPipeline();

//         // Swap trigger (index 0) with LoadAudience (index 1)
//         (steps[0], steps[1]) = (steps[1], steps[0]);

//         var ex = Assert.Throws<InvalidOperationException>(() =>
//             new WorkflowDefinitionAggregate(NewVersionId(), NewWorkflowId(), steps));
//         Assert.Contains("first step in a workflow definition must be a trigger step", ex.Message);
//     }

//     // ──────────────────────────────────────────────────────────────────────
//     // Error 3: Multiple trigger steps (§8.6 rule 1)
//     // ──────────────────────────────────────────────────────────────────────
//     [Fact]
//     public void Marketing_MultipleTriggerSteps_Fails()
//     {
//         var (steps, ids) = BuildValidMarketingPipeline();

//         steps.Add(new TriggerStepDefinition(NewStepId(), "ManualLaunch", ids.IntegrationId, "manual.launch",
//             new Dictionary<string, string> { ["mode"] = "manual" }, ids.S2,
//             Schema(("campaignId", "string"), ("budget", "decimal"))));

//         var ex = Assert.Throws<InvalidOperationException>(() =>
//             new WorkflowDefinitionAggregate(NewVersionId(), NewWorkflowId(), steps));
//         Assert.Contains("exactly one trigger step", ex.Message);
//     }

//     // ──────────────────────────────────────────────────────────────────────
//     // Error 4: Action step has null OutputSchema (§8.6 rule 10)
//     // ──────────────────────────────────────────────────────────────────────
//     [Fact]
//     public void Marketing_ActionNullOutputSchema_Fails()
//     {
//         var (steps, ids) = BuildValidMarketingPipeline();

//         // Replace LoadAudience (index 1) with null OutputSchema
//         steps[1] = new ActionStepDefinition(ids.S2, "LoadAudience", ids.IntegrationId, "audience.load",
//             new Dictionary<string, TemplateOrLiteral>
//             {
//                 ["campaignId"] = TemplateOrLiteral.Template("{{CampaignLaunch.campaignId}}")
//             },
//             FailureStrategy.Stop,
//             outputSchema: null, nextStepId: ids.S3);

//         var ex = Assert.Throws<InvalidOperationException>(() =>
//             new WorkflowDefinitionAggregate(NewVersionId(), NewWorkflowId(), steps));
//         Assert.Contains("must have a non-null OutputSchema", ex.Message);
//     }

//     // ──────────────────────────────────────────────────────────────────────
//     // Error 5: NextStepId references a step not in the list (§8.6 rule 3)
//     // ──────────────────────────────────────────────────────────────────────
//     [Fact]
//     public void Marketing_NextStepIdReferencesNonExistentStep_Fails()
//     {
//         var (steps, ids) = BuildValidMarketingPipeline();
//         var phantomStepId = NewStepId();

//         // Replace BuildCreatives (index 3) to point to phantom step
//         steps[3] = new ActionStepDefinition(ids.S4, "BuildCreatives", ids.IntegrationId, "creative.build",
//             new Dictionary<string, TemplateOrLiteral>
//             {
//                 ["campaignId"] = TemplateOrLiteral.Template("{{CampaignLaunch.campaignId}}")
//             },
//             FailureStrategy.Retry, retryCount: 1,
//             outputSchema: Schema(("creativeIds", "array"), ("count", "int")), nextStepId: phantomStepId);

//         var ex = Assert.Throws<InvalidOperationException>(() =>
//             new WorkflowDefinitionAggregate(NewVersionId(), NewWorkflowId(), steps));
//         Assert.Contains("is referenced but does not exist in the workflow", ex.Message);
//     }

//     // ──────────────────────────────────────────────────────────────────────
//     // Error 6: Cycle detected — linear chain back-edge (§8.6 rule 2)
//     //          ArchiveCampaign chains back to LoadAudience in the main chain
//     // ──────────────────────────────────────────────────────────────────────
//     [Fact]
//     public void Marketing_CycleDetected_Fails()
//     {
//         var (steps, ids) = BuildValidMarketingPipeline();

//         // Replace ScheduleNext (index 15, last step) to point back to LoadAudience (s2),
//         // creating a linear cycle after all steps are visited: ... → s16 → s2
//         steps[15] = new ActionStepDefinition(ids.S16, "ScheduleNext", ids.IntegrationId, "campaign.schedule",
//             new Dictionary<string, TemplateOrLiteral>
//             {
//                 ["campaignId"] = TemplateOrLiteral.Template("{{CampaignLaunch.campaignId}}"),
//                 ["startDate"] = TemplateOrLiteral.Template("{{CampaignLaunch.startDate}}")
//             },
//             FailureStrategy.Skip,
//             outputSchema: Schema(("nextCampaignId", "string"), ("scheduledAt", "string")), nextStepId: ids.S2);

//         var ex = Assert.Throws<InvalidOperationException>(() =>
//             new WorkflowDefinitionAggregate(NewVersionId(), NewWorkflowId(), steps));
//         Assert.Contains("Cycle detected", ex.Message);
//     }

//     // ──────────────────────────────────────────────────────────────────────
//     // Error 7: Template references an unknown step name (§8.5.1)
//     // ──────────────────────────────────────────────────────────────────────
//     [Fact]
//     public void Marketing_TemplateReferencesUnknownStepName_Fails()
//     {
//         var (steps, ids) = BuildValidMarketingPipeline();

//         // Replace UpdateAnalytics (index 12): "DeliveryMerge" instead of "MergeDeliveryResults"
//         steps[12] = new ActionStepDefinition(ids.S13, "UpdateAnalytics", ids.IntegrationId, "analytics.update",
//             new Dictionary<string, TemplateOrLiteral>
//             {
//                 ["campaignId"] = TemplateOrLiteral.Template("{{CampaignLaunch.campaignId}}"),
//                 ["totalSent"] = TemplateOrLiteral.Template("{{DeliveryMerge.totalSent}}")
//             },
//             FailureStrategy.Skip,
//             outputSchema: Schema(("analyticsId", "string"), ("updatedAt", "string")), nextStepId: ids.S14);

//         var ex = Assert.Throws<InvalidOperationException>(() =>
//             new WorkflowDefinitionAggregate(NewVersionId(), NewWorkflowId(), steps));
//         Assert.Contains("references unknown step 'DeliveryMerge'", ex.Message);
//     }

//     // ──────────────────────────────────────────────────────────────────────
//     // Error 8: Template forward reference (§8.5.1)
//     //          LoadAudience references ReportROI which is downstream
//     // ──────────────────────────────────────────────────────────────────────
//     [Fact]
//     public void Marketing_TemplateForwardReference_Fails()
//     {
//         var (steps, ids) = BuildValidMarketingPipeline();

//         // Replace LoadAudience (index 1) to reference ReportROI which is far downstream
//         steps[1] = new ActionStepDefinition(ids.S2, "LoadAudience", ids.IntegrationId, "audience.load",
//             new Dictionary<string, TemplateOrLiteral>
//             {
//                 ["campaignId"] = TemplateOrLiteral.Template("{{CampaignLaunch.campaignId}}"),
//                 ["roi"] = TemplateOrLiteral.Template("{{ReportROI.roi}}")
//             },
//             FailureStrategy.Stop,
//             outputSchema: Schema(("audienceSize", "int"), ("segmentCount", "int")), nextStepId: ids.S3);

//         var ex = Assert.Throws<InvalidOperationException>(() =>
//             new WorkflowDefinitionAggregate(NewVersionId(), NewWorkflowId(), steps));
//         Assert.Contains("references step 'ReportROI' before it is guaranteed to complete", ex.Message);
//     }

//     // ──────────────────────────────────────────────────────────────────────
//     // Error 9: Template references a non-existent field (§8.5.2)
//     // ──────────────────────────────────────────────────────────────────────
//     [Fact]
//     public void Marketing_TemplateReferencesNonExistentField_Fails()
//     {
//         var (steps, ids) = BuildValidMarketingPipeline();

//         // Replace MergeDeliveryResults (index 8): "emailCount" instead of "sent" on EmailChannel
//         steps[8] = new ActionStepDefinition(ids.S9, "MergeDeliveryResults", ids.IntegrationId, "delivery.merge",
//             new Dictionary<string, TemplateOrLiteral>
//             {
//                 ["emailSent"] = TemplateOrLiteral.Template("{{EmailChannel.emailCount}}"),
//                 ["smsSent"] = TemplateOrLiteral.Template("{{SMSChannel.sent}}"),
//                 ["pushSent"] = TemplateOrLiteral.Template("{{PushChannel.sent}}")
//             },
//             FailureStrategy.Stop,
//             outputSchema: Schema(("totalSent", "int"), ("conversionRate", "decimal")), nextStepId: ids.S10);

//         var ex = Assert.Throws<InvalidOperationException>(() =>
//             new WorkflowDefinitionAggregate(NewVersionId(), NewWorkflowId(), steps));
//         Assert.Contains("references non-existent field 'emailCount' on step 'EmailChannel'", ex.Message);
//     }

//     // ──────────────────────────────────────────────────────────────────────
//     // Error 10: Orphaned unreachable steps (§8.6 rule 4)
//     //           Per requirements, even 1 unreachable step must be detected
//     // ──────────────────────────────────────────────────────────────────────
//     [Fact]
//     public void Marketing_OrphanedUnreachableSteps_Fails()
//     {
//         var (steps, ids) = BuildValidMarketingPipeline();

//         // Add one unreachable step — requirements say ALL steps must be reachable
//         steps.Add(new ActionStepDefinition(NewStepId(), "DanglingStep", ids.IntegrationId, "dangling.action",
//             new Dictionary<string, TemplateOrLiteral> { ["input"] = TemplateOrLiteral.Literal("data") },
//             FailureStrategy.Stop,
//             outputSchema: Schema(("result", "string"), ("status", "string"))));

//         var ex = Assert.Throws<InvalidOperationException>(() =>
//             new WorkflowDefinitionAggregate(NewVersionId(), NewWorkflowId(), steps));
//         Assert.Contains("unreachable (orphaned) steps", ex.Message);
//     }

//     // ──────────────────────────────────────────────────────────────────────
//     // Error 11: Condition branch last step has NextStepId != null (§8.6 rule 6)
//     //           "The last step in every condition branch must have NextStepId = null"
//     // ──────────────────────────────────────────────────────────────────────
//     [Fact]
//     public void Marketing_BranchLastStepNextStepIdNotNull_Fails()
//     {
//         var (steps, ids) = BuildValidMarketingPipeline();

//         // PauseCampaign (s12, index 11) is the last step of the second condition branch.
//         // It has NextStepId = null. Set it to point somewhere to violate rule 6.
//         steps[11] = new ActionStepDefinition(ids.S12, "PauseCampaign", ids.IntegrationId, "campaign.pause",
//             new Dictionary<string, TemplateOrLiteral>
//             {
//                 ["campaignId"] = TemplateOrLiteral.Template("{{CampaignLaunch.campaignId}}")
//             },
//             FailureStrategy.Stop,
//             outputSchema: Schema(("pausedAt", "string"), ("reason", "string")),
//             nextStepId: ids.S13);

//         var ex = Assert.Throws<InvalidOperationException>(() =>
//             new WorkflowDefinitionAggregate(NewVersionId(), NewWorkflowId(), steps));
//         Assert.Contains("NextStepId", ex.Message);
//     }

//     // ──────────────────────────────────────────────────────────────────────
//     // Error 12: Condition step with zero rules (§8.6 rule 7)
//     //           "A ConditionStepDefinition must have at least one ConditionRule"
//     // ──────────────────────────────────────────────────────────────────────
//     [Fact]
//     public void Marketing_ConditionWithZeroRules_Fails()
//     {
//         var (steps, ids) = BuildValidMarketingPipeline();

//         // Replace PerformanceCheck (s10, index 9) with empty rules
//         steps[9] = new ConditionStepDefinition(ids.S10, "PerformanceCheck",
//             new List<ConditionRule>(), nextStepId: ids.S13);

//         var ex = Assert.Throws<InvalidOperationException>(() =>
//             new WorkflowDefinitionAggregate(NewVersionId(), NewWorkflowId(), steps));
//         Assert.Contains("rule", ex.Message, StringComparison.OrdinalIgnoreCase);
//     }
// }

// /// <summary>
// /// 12 validation failure tests for the HR Offboarding pipeline (Scenario 7).
// /// 18-step pipeline with parallel access revocation (3 branches), loop (CollectAssets, 2 inner steps),
// /// condition (VoluntaryExit with 2-step and 1-step branches), and template references.
// /// Tests are derived from requirements §8.5 and §8.6, not from implementation.
// /// </summary>
// public class HrOffboardingValidationFailureTests
// {
//     private static StepId NewStepId() => new(Guid.NewGuid());
//     private static IntegrationId NewIntegrationId() => new(Guid.NewGuid());
//     private static WorkflowVersionId NewVersionId() => new(Guid.NewGuid());
//     private static WorkflowId NewWorkflowId() => new(Guid.NewGuid());

//     private static StepOutputSchema Schema(params (string key, string type)[] fields)
//     {
//         var dict = new Dictionary<string, string>();
//         foreach (var (key, type) in fields)
//             dict[key] = type;
//         return new StepOutputSchema(dict);
//     }

//     private record OffboardingIds(
//         IntegrationId IntegrationId,
//         StepId S1, StepId S2, StepId S3, StepId S4, StepId S5, StepId S6,
//         StepId S7, StepId S7Inner1, StepId S7Inner2,
//         StepId S8, StepId S9, StepId S10, StepId S11, StepId S12,
//         StepId S13, StepId S14, StepId S15, StepId S16, StepId S17, StepId S18);

//     private static (List<StepDefinition> steps, OffboardingIds ids) BuildValidOffboardingPipeline()
//     {
//         var integrationId = NewIntegrationId();
//         var s1 = NewStepId();
//         var s2 = NewStepId();
//         var s3 = NewStepId();
//         var s4 = NewStepId();
//         var s5 = NewStepId();
//         var s6 = NewStepId();
//         var s7 = NewStepId();
//         var s7Inner1 = NewStepId();
//         var s7Inner2 = NewStepId();
//         var s8 = NewStepId();
//         var s9 = NewStepId();
//         var s10 = NewStepId();
//         var s11 = NewStepId();
//         var s12 = NewStepId();
//         var s13 = NewStepId();
//         var s14 = NewStepId();
//         var s15 = NewStepId();
//         var s16 = NewStepId();
//         var s17 = NewStepId();
//         var s18 = NewStepId();

//         var loopInnerSteps = new List<StepDefinition>
//         {
//             new ActionStepDefinition(s7Inner1, "TrackAsset", integrationId, "asset.track",
//                 new Dictionary<string, TemplateOrLiteral>
//                 {
//                     ["assetId"] = TemplateOrLiteral.Literal("currentAsset")
//                 },
//                 FailureStrategy.Skip,
//                 outputSchema: Schema(("assetType", "string"), ("location", "string")), nextStepId: s7Inner2),

//             new ActionStepDefinition(s7Inner2, "ConfirmReturn", integrationId, "asset.confirm",
//                 new Dictionary<string, TemplateOrLiteral>
//                 {
//                     ["assetType"] = TemplateOrLiteral.Literal("laptop")
//                 },
//                 FailureStrategy.Skip,
//                 outputSchema: Schema(("returned", "bool"), ("condition", "string")))
//         };

//         var steps = new List<StepDefinition>
//         {
//             new TriggerStepDefinition(s1, "OffboardingInitiated", integrationId, "hr.offboard",
//                 new Dictionary<string, string> { ["source"] = "hris" }, s2,
//                 Schema(("employeeId", "string"), ("department", "string"), ("exitType", "string"), ("assets", "array"))),

//             new ActionStepDefinition(s2, "InitOffboarding", integrationId, "offboard.init",
//                 new Dictionary<string, TemplateOrLiteral>
//                 {
//                     ["employeeId"] = TemplateOrLiteral.Template("{{OffboardingInitiated.employeeId}}"),
//                     ["department"] = TemplateOrLiteral.Template("{{OffboardingInitiated.department}}")
//                 },
//                 FailureStrategy.Stop,
//                 outputSchema: Schema(("caseId", "string"), ("lastDay", "string")), nextStepId: s3),

//             new ParallelStepDefinition(s3, "RevokeAccess",
//                 new List<StepId> { s4, s5, s6 }, nextStepId: s7),

//             new ActionStepDefinition(s4, "RevokeEmail", integrationId, "google.revoke",
//                 new Dictionary<string, TemplateOrLiteral>
//                 {
//                     ["employeeId"] = TemplateOrLiteral.Template("{{OffboardingInitiated.employeeId}}")
//                 },
//                 FailureStrategy.Retry, retryCount: 2,
//                 outputSchema: Schema(("revoked", "bool"), ("revokedAt", "string"))),

//             new ActionStepDefinition(s5, "RevokeVPN", integrationId, "vpn.revoke",
//                 new Dictionary<string, TemplateOrLiteral>
//                 {
//                     ["employeeId"] = TemplateOrLiteral.Template("{{OffboardingInitiated.employeeId}}")
//                 },
//                 FailureStrategy.Retry, retryCount: 2,
//                 outputSchema: Schema(("revoked", "bool"), ("revokedAt", "string"))),

//             new ActionStepDefinition(s6, "RevokeBadge", integrationId, "badge.revoke",
//                 new Dictionary<string, TemplateOrLiteral>
//                 {
//                     ["employeeId"] = TemplateOrLiteral.Template("{{OffboardingInitiated.employeeId}}")
//                 },
//                 FailureStrategy.Skip,
//                 outputSchema: Schema(("revoked", "bool"), ("badgeId", "string"))),

//             new LoopStepDefinition(s7, "CollectAssets",
//                 new TemplateReference("{{OffboardingInitiated.assets}}"),
//                 Schema(("currentAsset", "string")),
//                 loopInnerSteps,
//                 ConcurrencyMode.Sequential,
//                 IterationFailureStrategy.Skip,
//                 retryCount: 0, nextStepId: s8),

//             new ConditionStepDefinition(s8, "VoluntaryExit",
//                 new List<ConditionRule>
//                 {
//                     new("{{OffboardingInitiated.exitType}} == 'voluntary'", s9),
//                     new("{{OffboardingInitiated.exitType}} == 'involuntary'", s11)
//                 }, nextStepId: s12),

//             new ActionStepDefinition(s9, "ExitInterview", integrationId, "interview.schedule",
//                 new Dictionary<string, TemplateOrLiteral>
//                 {
//                     ["employeeId"] = TemplateOrLiteral.Template("{{OffboardingInitiated.employeeId}}")
//                 },
//                 FailureStrategy.Skip,
//                 outputSchema: Schema(("interviewId", "string"), ("scheduledAt", "string")), nextStepId: s10),

//             new ActionStepDefinition(s10, "FeedbackSurvey", integrationId, "survey.send",
//                 new Dictionary<string, TemplateOrLiteral>
//                 {
//                     ["employeeId"] = TemplateOrLiteral.Template("{{OffboardingInitiated.employeeId}}")
//                 },
//                 FailureStrategy.Skip,
//                 outputSchema: Schema(("surveyId", "string"), ("sentAt", "string"))),

//             new ActionStepDefinition(s11, "LegalReview", integrationId, "legal.review",
//                 new Dictionary<string, TemplateOrLiteral>
//                 {
//                     ["employeeId"] = TemplateOrLiteral.Template("{{OffboardingInitiated.employeeId}}"),
//                     ["caseId"] = TemplateOrLiteral.Template("{{InitOffboarding.caseId}}")
//                 },
//                 FailureStrategy.Stop,
//                 outputSchema: Schema(("reviewId", "string"), ("clearance", "bool"))),

//             new ActionStepDefinition(s12, "FinalPaycheck", integrationId, "payroll.final",
//                 new Dictionary<string, TemplateOrLiteral>
//                 {
//                     ["employeeId"] = TemplateOrLiteral.Template("{{OffboardingInitiated.employeeId}}"),
//                     ["lastDay"] = TemplateOrLiteral.Template("{{InitOffboarding.lastDay}}")
//                 },
//                 FailureStrategy.Stop,
//                 outputSchema: Schema(("paycheckId", "string"), ("amount", "decimal")), nextStepId: s13),

//             new ActionStepDefinition(s13, "ProcessBenefits", integrationId, "benefits.terminate",
//                 new Dictionary<string, TemplateOrLiteral>
//                 {
//                     ["employeeId"] = TemplateOrLiteral.Template("{{OffboardingInitiated.employeeId}}")
//                 },
//                 FailureStrategy.Stop,
//                 outputSchema: Schema(("benefitId", "string"), ("cobraEligible", "bool")), nextStepId: s14),

//             new ActionStepDefinition(s14, "ArchiveProfile", integrationId, "profile.archive",
//                 new Dictionary<string, TemplateOrLiteral>
//                 {
//                     ["employeeId"] = TemplateOrLiteral.Template("{{OffboardingInitiated.employeeId}}")
//                 },
//                 FailureStrategy.Skip,
//                 outputSchema: Schema(("archiveId", "string"), ("archivedAt", "string")), nextStepId: s15),

//             new ActionStepDefinition(s15, "NotifyTeam", integrationId, "slack.notify",
//                 new Dictionary<string, TemplateOrLiteral>
//                 {
//                     ["department"] = TemplateOrLiteral.Template("{{OffboardingInitiated.department}}")
//                 },
//                 FailureStrategy.Skip,
//                 outputSchema: Schema(("messageId", "string"), ("sentAt", "string")), nextStepId: s16),

//             new ActionStepDefinition(s16, "UpdateOrgChart", integrationId, "org.update",
//                 new Dictionary<string, TemplateOrLiteral>
//                 {
//                     ["employeeId"] = TemplateOrLiteral.Template("{{OffboardingInitiated.employeeId}}")
//                 },
//                 FailureStrategy.Skip,
//                 outputSchema: Schema(("updatedAt", "string"), ("chartVersion", "string")), nextStepId: s17),

//             new ActionStepDefinition(s17, "CloseCase", integrationId, "case.close",
//                 new Dictionary<string, TemplateOrLiteral>
//                 {
//                     ["caseId"] = TemplateOrLiteral.Template("{{InitOffboarding.caseId}}")
//                 },
//                 FailureStrategy.Stop,
//                 outputSchema: Schema(("closedAt", "string"), ("status", "string")), nextStepId: s18),

//             new ActionStepDefinition(s18, "ComplianceLog", integrationId, "compliance.log",
//                 new Dictionary<string, TemplateOrLiteral>
//                 {
//                     ["employeeId"] = TemplateOrLiteral.Template("{{OffboardingInitiated.employeeId}}"),
//                     ["caseId"] = TemplateOrLiteral.Template("{{InitOffboarding.caseId}}")
//                 },
//                 FailureStrategy.Stop,
//                 outputSchema: Schema(("logId", "string"), ("timestamp", "string")))
//         };

//         return (steps, new OffboardingIds(integrationId, s1, s2, s3, s4, s5, s6,
//             s7, s7Inner1, s7Inner2, s8, s9, s10, s11, s12, s13, s14, s15, s16, s17, s18));
//     }

//     // ──────────────────────────────────────────────────────────────────────
//     // Error 1: Trigger step has null OutputSchema (§8.6 rule 10)
//     // ──────────────────────────────────────────────────────────────────────
//     [Fact]
//     public void Offboarding_TriggerNullOutputSchema_Fails()
//     {
//         var (steps, ids) = BuildValidOffboardingPipeline();

//         steps[0] = new TriggerStepDefinition(ids.S1, "OffboardingInitiated", ids.IntegrationId, "hr.offboard",
//             new Dictionary<string, string> { ["source"] = "hris" }, ids.S2,
//             outputSchema: null);

//         var ex = Assert.Throws<InvalidOperationException>(() =>
//             new WorkflowDefinitionAggregate(NewVersionId(), NewWorkflowId(), steps));
//         Assert.Contains("must have a non-null OutputSchema", ex.Message);
//     }

//     // ──────────────────────────────────────────────────────────────────────
//     // Error 2: Trigger is not the first step (§8.6 rule 1)
//     // ──────────────────────────────────────────────────────────────────────
//     [Fact]
//     public void Offboarding_TriggerNotFirstStep_Fails()
//     {
//         var (steps, ids) = BuildValidOffboardingPipeline();

//         (steps[0], steps[1]) = (steps[1], steps[0]);

//         var ex = Assert.Throws<InvalidOperationException>(() =>
//             new WorkflowDefinitionAggregate(NewVersionId(), NewWorkflowId(), steps));
//         Assert.Contains("first step in a workflow definition must be a trigger step", ex.Message);
//     }

//     // ──────────────────────────────────────────────────────────────────────
//     // Error 3: Multiple trigger steps (§8.6 rule 1)
//     // ──────────────────────────────────────────────────────────────────────
//     [Fact]
//     public void Offboarding_MultipleTriggerSteps_Fails()
//     {
//         var (steps, ids) = BuildValidOffboardingPipeline();

//         steps.Add(new TriggerStepDefinition(NewStepId(), "ManualOffboard", ids.IntegrationId, "manual.offboard",
//             new Dictionary<string, string> { ["mode"] = "manual" }, ids.S2,
//             Schema(("employeeId", "string"), ("department", "string"))));

//         var ex = Assert.Throws<InvalidOperationException>(() =>
//             new WorkflowDefinitionAggregate(NewVersionId(), NewWorkflowId(), steps));
//         Assert.Contains("exactly one trigger step", ex.Message);
//     }

//     // ──────────────────────────────────────────────────────────────────────
//     // Error 4: Action step has null OutputSchema (§8.6 rule 10)
//     // ──────────────────────────────────────────────────────────────────────
//     [Fact]
//     public void Offboarding_ActionNullOutputSchema_Fails()
//     {
//         var (steps, ids) = BuildValidOffboardingPipeline();

//         steps[1] = new ActionStepDefinition(ids.S2, "InitOffboarding", ids.IntegrationId, "offboard.init",
//             new Dictionary<string, TemplateOrLiteral>
//             {
//                 ["employeeId"] = TemplateOrLiteral.Template("{{OffboardingInitiated.employeeId}}"),
//                 ["department"] = TemplateOrLiteral.Template("{{OffboardingInitiated.department}}")
//             },
//             FailureStrategy.Stop,
//             outputSchema: null, nextStepId: ids.S3);

//         var ex = Assert.Throws<InvalidOperationException>(() =>
//             new WorkflowDefinitionAggregate(NewVersionId(), NewWorkflowId(), steps));
//         Assert.Contains("must have a non-null OutputSchema", ex.Message);
//     }

//     // ──────────────────────────────────────────────────────────────────────
//     // Error 5: NextStepId references a step not in the list (§8.6 rule 3)
//     // ──────────────────────────────────────────────────────────────────────
//     [Fact]
//     public void Offboarding_NextStepIdReferencesNonExistentStep_Fails()
//     {
//         var (steps, ids) = BuildValidOffboardingPipeline();
//         var phantomStepId = NewStepId();

//         steps[11] = new ActionStepDefinition(ids.S12, "FinalPaycheck", ids.IntegrationId, "payroll.final",
//             new Dictionary<string, TemplateOrLiteral>
//             {
//                 ["employeeId"] = TemplateOrLiteral.Template("{{OffboardingInitiated.employeeId}}"),
//                 ["lastDay"] = TemplateOrLiteral.Template("{{InitOffboarding.lastDay}}")
//             },
//             FailureStrategy.Stop,
//             outputSchema: Schema(("paycheckId", "string"), ("amount", "decimal")), nextStepId: phantomStepId);

//         var ex = Assert.Throws<InvalidOperationException>(() =>
//             new WorkflowDefinitionAggregate(NewVersionId(), NewWorkflowId(), steps));
//         Assert.Contains("is referenced but does not exist in the workflow", ex.Message);
//     }

//     // ──────────────────────────────────────────────────────────────────────
//     // Error 6: Cycle detected — linear chain back-edge (§8.6 rule 2)
//     //          UpdateOrgChart chains back to FinalPaycheck in the main chain
//     // ──────────────────────────────────────────────────────────────────────
//     [Fact]
//     public void Offboarding_CycleDetected_Fails()
//     {
//         var (steps, ids) = BuildValidOffboardingPipeline();

//         steps[15] = new ActionStepDefinition(ids.S16, "UpdateOrgChart", ids.IntegrationId, "org.update",
//             new Dictionary<string, TemplateOrLiteral>
//             {
//                 ["employeeId"] = TemplateOrLiteral.Template("{{OffboardingInitiated.employeeId}}")
//             },
//             FailureStrategy.Skip,
//             outputSchema: Schema(("updatedAt", "string"), ("chartVersion", "string")), nextStepId: ids.S12);

//         var ex = Assert.Throws<InvalidOperationException>(() =>
//             new WorkflowDefinitionAggregate(NewVersionId(), NewWorkflowId(), steps));
//         Assert.Contains("Cycle detected", ex.Message);
//     }

//     // ──────────────────────────────────────────────────────────────────────
//     // Error 7: Template references an unknown step name (§8.5.1)
//     // ──────────────────────────────────────────────────────────────────────
//     [Fact]
//     public void Offboarding_TemplateReferencesUnknownStepName_Fails()
//     {
//         var (steps, ids) = BuildValidOffboardingPipeline();

//         steps[16] = new ActionStepDefinition(ids.S17, "CloseCase", ids.IntegrationId, "case.close",
//             new Dictionary<string, TemplateOrLiteral>
//             {
//                 ["caseId"] = TemplateOrLiteral.Template("{{OffboardInit.caseId}}")
//             },
//             FailureStrategy.Stop,
//             outputSchema: Schema(("closedAt", "string"), ("status", "string")), nextStepId: ids.S18);

//         var ex = Assert.Throws<InvalidOperationException>(() =>
//             new WorkflowDefinitionAggregate(NewVersionId(), NewWorkflowId(), steps));
//         Assert.Contains("references unknown step 'OffboardInit'", ex.Message);
//     }

//     // ──────────────────────────────────────────────────────────────────────
//     // Error 8: Template forward reference (§8.5.1)
//     //          InitOffboarding references CloseCase which is downstream
//     // ──────────────────────────────────────────────────────────────────────
//     [Fact]
//     public void Offboarding_TemplateForwardReference_Fails()
//     {
//         var (steps, ids) = BuildValidOffboardingPipeline();

//         steps[1] = new ActionStepDefinition(ids.S2, "InitOffboarding", ids.IntegrationId, "offboard.init",
//             new Dictionary<string, TemplateOrLiteral>
//             {
//                 ["employeeId"] = TemplateOrLiteral.Template("{{OffboardingInitiated.employeeId}}"),
//                 ["closedAt"] = TemplateOrLiteral.Template("{{CloseCase.closedAt}}")
//             },
//             FailureStrategy.Stop,
//             outputSchema: Schema(("caseId", "string"), ("lastDay", "string")), nextStepId: ids.S3);

//         var ex = Assert.Throws<InvalidOperationException>(() =>
//             new WorkflowDefinitionAggregate(NewVersionId(), NewWorkflowId(), steps));
//         Assert.Contains("references step 'CloseCase' before it is guaranteed to complete", ex.Message);
//     }

//     // ──────────────────────────────────────────────────────────────────────
//     // Error 9: Template references a non-existent field (§8.5.2)
//     // ──────────────────────────────────────────────────────────────────────
//     [Fact]
//     public void Offboarding_TemplateReferencesNonExistentField_Fails()
//     {
//         var (steps, ids) = BuildValidOffboardingPipeline();

//         steps[17] = new ActionStepDefinition(ids.S18, "ComplianceLog", ids.IntegrationId, "compliance.log",
//             new Dictionary<string, TemplateOrLiteral>
//             {
//                 ["employeeId"] = TemplateOrLiteral.Template("{{OffboardingInitiated.employeeId}}"),
//                 ["caseId"] = TemplateOrLiteral.Template("{{InitOffboarding.ticketId}}")
//             },
//             FailureStrategy.Stop,
//             outputSchema: Schema(("logId", "string"), ("timestamp", "string")));

//         var ex = Assert.Throws<InvalidOperationException>(() =>
//             new WorkflowDefinitionAggregate(NewVersionId(), NewWorkflowId(), steps));
//         Assert.Contains("references non-existent field 'ticketId' on step 'InitOffboarding'", ex.Message);
//     }

//     // ──────────────────────────────────────────────────────────────────────
//     // Error 10: Orphaned unreachable steps (§8.6 rule 4)
//     //           Per requirements, even 1 unreachable step must be detected
//     // ──────────────────────────────────────────────────────────────────────
//     [Fact]
//     public void Offboarding_OrphanedUnreachableSteps_Fails()
//     {
//         var (steps, ids) = BuildValidOffboardingPipeline();

//         steps.Add(new ActionStepDefinition(NewStepId(), "DanglingStep", ids.IntegrationId, "dangling.action",
//             new Dictionary<string, TemplateOrLiteral> { ["input"] = TemplateOrLiteral.Literal("data") },
//             FailureStrategy.Stop,
//             outputSchema: Schema(("result", "string"), ("status", "string"))));

//         var ex = Assert.Throws<InvalidOperationException>(() =>
//             new WorkflowDefinitionAggregate(NewVersionId(), NewWorkflowId(), steps));
//         Assert.Contains("unreachable (orphaned) steps", ex.Message);
//     }

//     // ──────────────────────────────────────────────────────────────────────
//     // Error 11: Condition branch last step has NextStepId != null (§8.6 rule 6)
//     //           "The last step in every condition branch must have NextStepId = null"
//     // ──────────────────────────────────────────────────────────────────────
//     [Fact]
//     public void Offboarding_BranchLastStepNextStepIdNotNull_Fails()
//     {
//         var (steps, ids) = BuildValidOffboardingPipeline();

//         // LegalReview (s11, index 10) is the last step of the "involuntary" condition branch.
//         steps[10] = new ActionStepDefinition(ids.S11, "LegalReview", ids.IntegrationId, "legal.review",
//             new Dictionary<string, TemplateOrLiteral>
//             {
//                 ["employeeId"] = TemplateOrLiteral.Template("{{OffboardingInitiated.employeeId}}"),
//                 ["caseId"] = TemplateOrLiteral.Template("{{InitOffboarding.caseId}}")
//             },
//             FailureStrategy.Stop,
//             outputSchema: Schema(("reviewId", "string"), ("clearance", "bool")),
//             nextStepId: ids.S12);

//         var ex = Assert.Throws<InvalidOperationException>(() =>
//             new WorkflowDefinitionAggregate(NewVersionId(), NewWorkflowId(), steps));
//         Assert.Contains("NextStepId", ex.Message);
//     }

//     // ──────────────────────────────────────────────────────────────────────
//     // Error 12: Condition step with zero rules (§8.6 rule 7)
//     //           "A ConditionStepDefinition must have at least one ConditionRule"
//     // ──────────────────────────────────────────────────────────────────────
//     [Fact]
//     public void Offboarding_ConditionWithZeroRules_Fails()
//     {
//         var (steps, ids) = BuildValidOffboardingPipeline();

//         steps[7] = new ConditionStepDefinition(ids.S8, "VoluntaryExit",
//             new List<ConditionRule>(), nextStepId: ids.S12);

//         var ex = Assert.Throws<InvalidOperationException>(() =>
//             new WorkflowDefinitionAggregate(NewVersionId(), NewWorkflowId(), steps));
//         Assert.Contains("rule", ex.Message, StringComparison.OrdinalIgnoreCase);
//     }
// }

// /// <summary>
// /// 12 validation failure tests for the IoT Sensor Monitoring pipeline (Scenario 8).
// /// 18-step pipeline with parallel ingestion (2 branches), loop (DetectAnomalies, 2 inner steps),
// /// condition (AnomalyLevel with critical 2-step and warning 1-step branches), and template references.
// /// Tests are derived from requirements §8.5 and §8.6, not from implementation.
// /// </summary>
// public class IoTSensorValidationFailureTests
// {
//     private static StepId NewStepId() => new(Guid.NewGuid());
//     private static IntegrationId NewIntegrationId() => new(Guid.NewGuid());
//     private static WorkflowVersionId NewVersionId() => new(Guid.NewGuid());
//     private static WorkflowId NewWorkflowId() => new(Guid.NewGuid());

//     private static StepOutputSchema Schema(params (string key, string type)[] fields)
//     {
//         var dict = new Dictionary<string, string>();
//         foreach (var (key, type) in fields)
//             dict[key] = type;
//         return new StepOutputSchema(dict);
//     }

//     private record IoTIds(
//         IntegrationId IntegrationId,
//         StepId S1, StepId S2, StepId S3, StepId S4, StepId S5,
//         StepId S6, StepId S6Inner1, StepId S6Inner2,
//         StepId S7, StepId S8, StepId S9, StepId S10, StepId S11, StepId S12,
//         StepId S13, StepId S14, StepId S15, StepId S16, StepId S17, StepId S18);

//     private static (List<StepDefinition> steps, IoTIds ids) BuildValidIoTPipeline()
//     {
//         var integrationId = NewIntegrationId();
//         var s1 = NewStepId();
//         var s2 = NewStepId();
//         var s3 = NewStepId();
//         var s4 = NewStepId();
//         var s5 = NewStepId();
//         var s6 = NewStepId();
//         var s6Inner1 = NewStepId();
//         var s6Inner2 = NewStepId();
//         var s7 = NewStepId();
//         var s8 = NewStepId();
//         var s9 = NewStepId();
//         var s10 = NewStepId();
//         var s11 = NewStepId();
//         var s12 = NewStepId();
//         var s13 = NewStepId();
//         var s14 = NewStepId();
//         var s15 = NewStepId();
//         var s16 = NewStepId();
//         var s17 = NewStepId();
//         var s18 = NewStepId();

//         var loopInnerSteps = new List<StepDefinition>
//         {
//             new ActionStepDefinition(s6Inner1, "AnalyzeReading", integrationId, "sensor.analyze",
//                 new Dictionary<string, TemplateOrLiteral>
//                 {
//                     ["reading"] = TemplateOrLiteral.Literal("currentReading")
//                 },
//                 FailureStrategy.Skip,
//                 outputSchema: Schema(("deviation", "decimal"), ("baseline", "decimal")), nextStepId: s6Inner2),

//             new ActionStepDefinition(s6Inner2, "ScoreAnomaly", integrationId, "anomaly.score",
//                 new Dictionary<string, TemplateOrLiteral>
//                 {
//                     ["deviation"] = TemplateOrLiteral.Literal("0.5")
//                 },
//                 FailureStrategy.Skip,
//                 outputSchema: Schema(("anomalyScore", "decimal"), ("classification", "string")))
//         };

//         var steps = new List<StepDefinition>
//         {
//             new TriggerStepDefinition(s1, "SensorBatchReceived", integrationId, "iot.batch",
//                 new Dictionary<string, string> { ["protocol"] = "mqtt" }, s2,
//                 Schema(("batchId", "string"), ("deviceId", "string"), ("readings", "array"), ("timestamp", "string"))),

//             new ParallelStepDefinition(s2, "IngestStreams",
//                 new List<StepId> { s3, s4 }, nextStepId: s5),

//             new ActionStepDefinition(s3, "IngestTemperature", integrationId, "timeseries.ingest",
//                 new Dictionary<string, TemplateOrLiteral>
//                 {
//                     ["batchId"] = TemplateOrLiteral.Template("{{SensorBatchReceived.batchId}}"),
//                     ["type"] = TemplateOrLiteral.Literal("temperature")
//                 },
//                 FailureStrategy.Retry, retryCount: 3,
//                 outputSchema: Schema(("recordCount", "int"), ("avgValue", "decimal"))),

//             new ActionStepDefinition(s4, "IngestHumidity", integrationId, "timeseries.ingest",
//                 new Dictionary<string, TemplateOrLiteral>
//                 {
//                     ["batchId"] = TemplateOrLiteral.Template("{{SensorBatchReceived.batchId}}"),
//                     ["type"] = TemplateOrLiteral.Literal("humidity")
//                 },
//                 FailureStrategy.Retry, retryCount: 3,
//                 outputSchema: Schema(("recordCount", "int"), ("avgValue", "decimal"))),

//             new ActionStepDefinition(s5, "CorrelateData", integrationId, "data.correlate",
//                 new Dictionary<string, TemplateOrLiteral>
//                 {
//                     ["tempAvg"] = TemplateOrLiteral.Template("{{IngestTemperature.avgValue}}"),
//                     ["humidityAvg"] = TemplateOrLiteral.Template("{{IngestHumidity.avgValue}}")
//                 },
//                 FailureStrategy.Stop,
//                 outputSchema: Schema(("correlationId", "string"), ("anomalyCount", "int")), nextStepId: s6),

//             new LoopStepDefinition(s6, "DetectAnomalies",
//                 new TemplateReference("{{SensorBatchReceived.readings}}"),
//                 Schema(("currentReading", "string")),
//                 loopInnerSteps,
//                 ConcurrencyMode.Parallel,
//                 IterationFailureStrategy.Skip,
//                 retryCount: 0, nextStepId: s7, maxConcurrency: 20),

//             new ConditionStepDefinition(s7, "AnomalyLevel",
//                 new List<ConditionRule>
//                 {
//                     new("{{CorrelateData.anomalyCount}} > 10", s8),
//                     new("{{CorrelateData.anomalyCount}} <= 10 && {{CorrelateData.anomalyCount}} > 0", s10)
//                 },
//                 nextStepId: s11, fallbackStepId: s11),

//             new ActionStepDefinition(s8, "ShutdownDevice", integrationId, "device.shutdown",
//                 new Dictionary<string, TemplateOrLiteral>
//                 {
//                     ["deviceId"] = TemplateOrLiteral.Template("{{SensorBatchReceived.deviceId}}")
//                 },
//                 FailureStrategy.Stop,
//                 outputSchema: Schema(("shutdownAt", "string"), ("status", "string")), nextStepId: s9),

//             new ActionStepDefinition(s9, "EmergencyAlert", integrationId, "alert.emergency",
//                 new Dictionary<string, TemplateOrLiteral>
//                 {
//                     ["deviceId"] = TemplateOrLiteral.Template("{{SensorBatchReceived.deviceId}}"),
//                     ["anomalyCount"] = TemplateOrLiteral.Template("{{CorrelateData.anomalyCount}}")
//                 },
//                 FailureStrategy.Retry, retryCount: 3,
//                 outputSchema: Schema(("alertId", "string"), ("severity", "string"))),

//             new ActionStepDefinition(s10, "ThrottleDevice", integrationId, "device.throttle",
//                 new Dictionary<string, TemplateOrLiteral>
//                 {
//                     ["deviceId"] = TemplateOrLiteral.Template("{{SensorBatchReceived.deviceId}}")
//                 },
//                 FailureStrategy.Skip,
//                 outputSchema: Schema(("throttledAt", "string"), ("newRate", "int"))),

//             new ActionStepDefinition(s11, "StoreTimeSeries", integrationId, "timeseries.store",
//                 new Dictionary<string, TemplateOrLiteral>
//                 {
//                     ["correlationId"] = TemplateOrLiteral.Template("{{CorrelateData.correlationId}}")
//                 },
//                 FailureStrategy.Retry, retryCount: 2,
//                 outputSchema: Schema(("storedCount", "int"), ("partitionKey", "string")), nextStepId: s12),

//             new ActionStepDefinition(s12, "UpdateDashboard", integrationId, "dashboard.refresh",
//                 new Dictionary<string, TemplateOrLiteral>
//                 {
//                     ["deviceId"] = TemplateOrLiteral.Template("{{SensorBatchReceived.deviceId}}")
//                 },
//                 FailureStrategy.Skip,
//                 outputSchema: Schema(("dashboardId", "string"), ("refreshedAt", "string")), nextStepId: s13),

//             new ActionStepDefinition(s13, "GenerateHeatmap", integrationId, "viz.heatmap",
//                 new Dictionary<string, TemplateOrLiteral>
//                 {
//                     ["batchId"] = TemplateOrLiteral.Template("{{SensorBatchReceived.batchId}}")
//                 },
//                 FailureStrategy.Skip,
//                 outputSchema: Schema(("heatmapUrl", "string"), ("generatedAt", "string")), nextStepId: s14),

//             new ActionStepDefinition(s14, "PredictMaintenance", integrationId, "ml.predict",
//                 new Dictionary<string, TemplateOrLiteral>
//                 {
//                     ["deviceId"] = TemplateOrLiteral.Template("{{SensorBatchReceived.deviceId}}"),
//                     ["correlationId"] = TemplateOrLiteral.Template("{{CorrelateData.correlationId}}")
//                 },
//                 FailureStrategy.Skip,
//                 outputSchema: Schema(("prediction", "string"), ("confidence", "decimal")), nextStepId: s15),

//             new ActionStepDefinition(s15, "ScheduleInspection", integrationId, "maintenance.schedule",
//                 new Dictionary<string, TemplateOrLiteral>
//                 {
//                     ["deviceId"] = TemplateOrLiteral.Template("{{SensorBatchReceived.deviceId}}"),
//                     ["prediction"] = TemplateOrLiteral.Template("{{PredictMaintenance.prediction}}")
//                 },
//                 FailureStrategy.Skip,
//                 outputSchema: Schema(("inspectionId", "string"), ("scheduledAt", "string")), nextStepId: s16),

//             new ActionStepDefinition(s16, "ExportReport", integrationId, "report.export",
//                 new Dictionary<string, TemplateOrLiteral>
//                 {
//                     ["batchId"] = TemplateOrLiteral.Template("{{SensorBatchReceived.batchId}}")
//                 },
//                 FailureStrategy.Skip,
//                 outputSchema: Schema(("reportUrl", "string"), ("format", "string")), nextStepId: s17),

//             new ActionStepDefinition(s17, "SyncCloud", integrationId, "cloud.sync",
//                 new Dictionary<string, TemplateOrLiteral>
//                 {
//                     ["batchId"] = TemplateOrLiteral.Template("{{SensorBatchReceived.batchId}}")
//                 },
//                 FailureStrategy.Skip,
//                 outputSchema: Schema(("syncId", "string"), ("syncedAt", "string")), nextStepId: s18),

//             new ActionStepDefinition(s18, "ArchiveBatch", integrationId, "batch.archive",
//                 new Dictionary<string, TemplateOrLiteral>
//                 {
//                     ["batchId"] = TemplateOrLiteral.Template("{{SensorBatchReceived.batchId}}")
//                 },
//                 FailureStrategy.Skip,
//                 outputSchema: Schema(("archiveId", "string"), ("archivedAt", "string")))
//         };

//         return (steps, new IoTIds(integrationId, s1, s2, s3, s4, s5,
//             s6, s6Inner1, s6Inner2, s7, s8, s9, s10, s11, s12, s13, s14, s15, s16, s17, s18));
//     }

//     // ──────────────────────────────────────────────────────────────────────
//     // Error 1: Trigger step has null OutputSchema (§8.6 rule 10)
//     // ──────────────────────────────────────────────────────────────────────
//     [Fact]
//     public void IoT_TriggerNullOutputSchema_Fails()
//     {
//         var (steps, ids) = BuildValidIoTPipeline();

//         steps[0] = new TriggerStepDefinition(ids.S1, "SensorBatchReceived", ids.IntegrationId, "iot.batch",
//             new Dictionary<string, string> { ["protocol"] = "mqtt" }, ids.S2,
//             outputSchema: null);

//         var ex = Assert.Throws<InvalidOperationException>(() =>
//             new WorkflowDefinitionAggregate(NewVersionId(), NewWorkflowId(), steps));
//         Assert.Contains("must have a non-null OutputSchema", ex.Message);
//     }

//     // ──────────────────────────────────────────────────────────────────────
//     // Error 2: Trigger is not the first step (§8.6 rule 1)
//     // ──────────────────────────────────────────────────────────────────────
//     [Fact]
//     public void IoT_TriggerNotFirstStep_Fails()
//     {
//         var (steps, ids) = BuildValidIoTPipeline();

//         (steps[0], steps[1]) = (steps[1], steps[0]);

//         var ex = Assert.Throws<InvalidOperationException>(() =>
//             new WorkflowDefinitionAggregate(NewVersionId(), NewWorkflowId(), steps));
//         Assert.Contains("first step in a workflow definition must be a trigger step", ex.Message);
//     }

//     // ──────────────────────────────────────────────────────────────────────
//     // Error 3: Multiple trigger steps (§8.6 rule 1)
//     // ──────────────────────────────────────────────────────────────────────
//     [Fact]
//     public void IoT_MultipleTriggerSteps_Fails()
//     {
//         var (steps, ids) = BuildValidIoTPipeline();

//         steps.Add(new TriggerStepDefinition(NewStepId(), "ManualBatch", ids.IntegrationId, "manual.batch",
//             new Dictionary<string, string> { ["mode"] = "manual" }, ids.S2,
//             Schema(("batchId", "string"), ("deviceId", "string"))));

//         var ex = Assert.Throws<InvalidOperationException>(() =>
//             new WorkflowDefinitionAggregate(NewVersionId(), NewWorkflowId(), steps));
//         Assert.Contains("exactly one trigger step", ex.Message);
//     }

//     // ──────────────────────────────────────────────────────────────────────
//     // Error 4: Action step has null OutputSchema (§8.6 rule 10)
//     // ──────────────────────────────────────────────────────────────────────
//     [Fact]
//     public void IoT_ActionNullOutputSchema_Fails()
//     {
//         var (steps, ids) = BuildValidIoTPipeline();

//         steps[4] = new ActionStepDefinition(ids.S5, "CorrelateData", ids.IntegrationId, "data.correlate",
//             new Dictionary<string, TemplateOrLiteral>
//             {
//                 ["tempAvg"] = TemplateOrLiteral.Template("{{IngestTemperature.avgValue}}"),
//                 ["humidityAvg"] = TemplateOrLiteral.Template("{{IngestHumidity.avgValue}}")
//             },
//             FailureStrategy.Stop,
//             outputSchema: null, nextStepId: ids.S6);

//         var ex = Assert.Throws<InvalidOperationException>(() =>
//             new WorkflowDefinitionAggregate(NewVersionId(), NewWorkflowId(), steps));
//         Assert.Contains("must have a non-null OutputSchema", ex.Message);
//     }

//     // ──────────────────────────────────────────────────────────────────────
//     // Error 5: NextStepId references a step not in the list (§8.6 rule 3)
//     // ──────────────────────────────────────────────────────────────────────
//     [Fact]
//     public void IoT_NextStepIdReferencesNonExistentStep_Fails()
//     {
//         var (steps, ids) = BuildValidIoTPipeline();
//         var phantomStepId = NewStepId();

//         steps[10] = new ActionStepDefinition(ids.S11, "StoreTimeSeries", ids.IntegrationId, "timeseries.store",
//             new Dictionary<string, TemplateOrLiteral>
//             {
//                 ["correlationId"] = TemplateOrLiteral.Template("{{CorrelateData.correlationId}}")
//             },
//             FailureStrategy.Retry, retryCount: 2,
//             outputSchema: Schema(("storedCount", "int"), ("partitionKey", "string")), nextStepId: phantomStepId);

//         var ex = Assert.Throws<InvalidOperationException>(() =>
//             new WorkflowDefinitionAggregate(NewVersionId(), NewWorkflowId(), steps));
//         Assert.Contains("is referenced but does not exist in the workflow", ex.Message);
//     }

//     // ──────────────────────────────────────────────────────────────────────
//     // Error 6: Cycle detected — linear chain back-edge (§8.6 rule 2)
//     //          SyncCloud chains back to StoreTimeSeries in the main chain
//     // ──────────────────────────────────────────────────────────────────────
//     [Fact]
//     public void IoT_CycleDetected_Fails()
//     {
//         var (steps, ids) = BuildValidIoTPipeline();

//         // Replace ArchiveBatch (index 17, last step) to point back to StoreTimeSeries (s11),
//         // creating a linear cycle after all steps are visited: ... → s18 → s11
//         steps[17] = new ActionStepDefinition(ids.S18, "ArchiveBatch", ids.IntegrationId, "batch.archive",
//             new Dictionary<string, TemplateOrLiteral>
//             {
//                 ["batchId"] = TemplateOrLiteral.Template("{{SensorBatchReceived.batchId}}")
//             },
//             FailureStrategy.Skip,
//             outputSchema: Schema(("archiveId", "string"), ("archivedAt", "string")), nextStepId: ids.S11);

//         var ex = Assert.Throws<InvalidOperationException>(() =>
//             new WorkflowDefinitionAggregate(NewVersionId(), NewWorkflowId(), steps));
//         Assert.Contains("Cycle detected", ex.Message);
//     }

//     // ──────────────────────────────────────────────────────────────────────
//     // Error 7: Template references an unknown step name (§8.5.1)
//     // ──────────────────────────────────────────────────────────────────────
//     [Fact]
//     public void IoT_TemplateReferencesUnknownStepName_Fails()
//     {
//         var (steps, ids) = BuildValidIoTPipeline();

//         steps[4] = new ActionStepDefinition(ids.S5, "CorrelateData", ids.IntegrationId, "data.correlate",
//             new Dictionary<string, TemplateOrLiteral>
//             {
//                 ["tempAvg"] = TemplateOrLiteral.Template("{{TempIngest.avgValue}}"),
//                 ["humidityAvg"] = TemplateOrLiteral.Template("{{IngestHumidity.avgValue}}")
//             },
//             FailureStrategy.Stop,
//             outputSchema: Schema(("correlationId", "string"), ("anomalyCount", "int")), nextStepId: ids.S6);

//         var ex = Assert.Throws<InvalidOperationException>(() =>
//             new WorkflowDefinitionAggregate(NewVersionId(), NewWorkflowId(), steps));
//         Assert.Contains("references unknown step 'TempIngest'", ex.Message);
//     }

//     // ──────────────────────────────────────────────────────────────────────
//     // Error 8: Template forward reference (§8.5.1)
//     //          CorrelateData references PredictMaintenance which is downstream
//     // ──────────────────────────────────────────────────────────────────────
//     [Fact]
//     public void IoT_TemplateForwardReference_Fails()
//     {
//         var (steps, ids) = BuildValidIoTPipeline();

//         steps[4] = new ActionStepDefinition(ids.S5, "CorrelateData", ids.IntegrationId, "data.correlate",
//             new Dictionary<string, TemplateOrLiteral>
//             {
//                 ["tempAvg"] = TemplateOrLiteral.Template("{{IngestTemperature.avgValue}}"),
//                 ["prediction"] = TemplateOrLiteral.Template("{{PredictMaintenance.prediction}}")
//             },
//             FailureStrategy.Stop,
//             outputSchema: Schema(("correlationId", "string"), ("anomalyCount", "int")), nextStepId: ids.S6);

//         var ex = Assert.Throws<InvalidOperationException>(() =>
//             new WorkflowDefinitionAggregate(NewVersionId(), NewWorkflowId(), steps));
//         Assert.Contains("references step 'PredictMaintenance' before it is guaranteed to complete", ex.Message);
//     }

//     // ──────────────────────────────────────────────────────────────────────
//     // Error 9: Template references a non-existent field (§8.5.2)
//     // ──────────────────────────────────────────────────────────────────────
//     [Fact]
//     public void IoT_TemplateReferencesNonExistentField_Fails()
//     {
//         var (steps, ids) = BuildValidIoTPipeline();

//         steps[17] = new ActionStepDefinition(ids.S18, "ArchiveBatch", ids.IntegrationId, "batch.archive",
//             new Dictionary<string, TemplateOrLiteral>
//             {
//                 ["batchId"] = TemplateOrLiteral.Template("{{SensorBatchReceived.sensorType}}")
//             },
//             FailureStrategy.Skip,
//             outputSchema: Schema(("archiveId", "string"), ("archivedAt", "string")));

//         var ex = Assert.Throws<InvalidOperationException>(() =>
//             new WorkflowDefinitionAggregate(NewVersionId(), NewWorkflowId(), steps));
//         Assert.Contains("references non-existent field 'sensorType' on step 'SensorBatchReceived'", ex.Message);
//     }

//     // ──────────────────────────────────────────────────────────────────────
//     // Error 10: Orphaned unreachable steps (§8.6 rule 4)
//     //           Per requirements, even 1 unreachable step must be detected
//     // ──────────────────────────────────────────────────────────────────────
//     [Fact]
//     public void IoT_OrphanedUnreachableSteps_Fails()
//     {
//         var (steps, ids) = BuildValidIoTPipeline();

//         steps.Add(new ActionStepDefinition(NewStepId(), "DanglingStep", ids.IntegrationId, "dangling.action",
//             new Dictionary<string, TemplateOrLiteral> { ["input"] = TemplateOrLiteral.Literal("data") },
//             FailureStrategy.Stop,
//             outputSchema: Schema(("result", "string"), ("status", "string"))));

//         var ex = Assert.Throws<InvalidOperationException>(() =>
//             new WorkflowDefinitionAggregate(NewVersionId(), NewWorkflowId(), steps));
//         Assert.Contains("unreachable (orphaned) steps", ex.Message);
//     }

//     // ──────────────────────────────────────────────────────────────────────
//     // Error 11: Condition branch last step has NextStepId != null (§8.6 rule 6)
//     //           "The last step in every condition branch must have NextStepId = null"
//     // ──────────────────────────────────────────────────────────────────────
//     [Fact]
//     public void IoT_BranchLastStepNextStepIdNotNull_Fails()
//     {
//         var (steps, ids) = BuildValidIoTPipeline();

//         // ThrottleDevice (s10, index 9) is the last step of the "warning" condition branch.
//         steps[9] = new ActionStepDefinition(ids.S10, "ThrottleDevice", ids.IntegrationId, "device.throttle",
//             new Dictionary<string, TemplateOrLiteral>
//             {
//                 ["deviceId"] = TemplateOrLiteral.Template("{{SensorBatchReceived.deviceId}}")
//             },
//             FailureStrategy.Skip,
//             outputSchema: Schema(("throttledAt", "string"), ("newRate", "int")),
//             nextStepId: ids.S11);

//         var ex = Assert.Throws<InvalidOperationException>(() =>
//             new WorkflowDefinitionAggregate(NewVersionId(), NewWorkflowId(), steps));
//         Assert.Contains("NextStepId", ex.Message);
//     }

//     // ──────────────────────────────────────────────────────────────────────
//     // Error 12: Condition step with zero rules (§8.6 rule 7)
//     //           "A ConditionStepDefinition must have at least one ConditionRule"
//     // ──────────────────────────────────────────────────────────────────────
//     [Fact]
//     public void IoT_ConditionWithZeroRules_Fails()
//     {
//         var (steps, ids) = BuildValidIoTPipeline();

//         steps[6] = new ConditionStepDefinition(ids.S7, "AnomalyLevel",
//             new List<ConditionRule>(), nextStepId: ids.S11);

//         var ex = Assert.Throws<InvalidOperationException>(() =>
//             new WorkflowDefinitionAggregate(NewVersionId(), NewWorkflowId(), steps));
//         Assert.Contains("rule", ex.Message, StringComparison.OrdinalIgnoreCase);
//     }
// }

// /// <summary>
// /// 12 validation failure tests for the Supply Chain Management pipeline (Scenario 9).
// /// 20-step pipeline with loop (ProcessLineItems, 2 inner steps), parallel (CheckSuppliers, 2 branches),
// /// two conditions (BestPrice and ExpediteCheck), and template references.
// /// Tests are derived from requirements §8.5 and §8.6, not from implementation.
// /// </summary>
// public class SupplyChainValidationFailureTests
// {
//     private static StepId NewStepId() => new(Guid.NewGuid());
//     private static IntegrationId NewIntegrationId() => new(Guid.NewGuid());
//     private static WorkflowVersionId NewVersionId() => new(Guid.NewGuid());
//     private static WorkflowId NewWorkflowId() => new(Guid.NewGuid());

//     private static StepOutputSchema Schema(params (string key, string type)[] fields)
//     {
//         var dict = new Dictionary<string, string>();
//         foreach (var (key, type) in fields)
//             dict[key] = type;
//         return new StepOutputSchema(dict);
//     }

//     private record SupplyChainIds(
//         IntegrationId IntegrationId,
//         StepId S1, StepId S2, StepId S3, StepId S3Inner1, StepId S3Inner2,
//         StepId S4, StepId S5, StepId S6, StepId S7, StepId S8, StepId S9, StepId S10,
//         StepId S11, StepId S12, StepId S13, StepId S14, StepId S15, StepId S16,
//         StepId S17, StepId S18, StepId S19, StepId S20);

//     private static (List<StepDefinition> steps, SupplyChainIds ids) BuildValidSupplyChainPipeline()
//     {
//         var integrationId = NewIntegrationId();
//         var s1 = NewStepId();
//         var s2 = NewStepId();
//         var s3 = NewStepId();
//         var s3Inner1 = NewStepId();
//         var s3Inner2 = NewStepId();
//         var s4 = NewStepId();
//         var s5 = NewStepId();
//         var s6 = NewStepId();
//         var s7 = NewStepId();
//         var s8 = NewStepId();
//         var s9 = NewStepId();
//         var s10 = NewStepId();
//         var s11 = NewStepId();
//         var s12 = NewStepId();
//         var s13 = NewStepId();
//         var s14 = NewStepId();
//         var s15 = NewStepId();
//         var s16 = NewStepId();
//         var s17 = NewStepId();
//         var s18 = NewStepId();
//         var s19 = NewStepId();
//         var s20 = NewStepId();

//         var loopInnerSteps = new List<StepDefinition>
//         {
//             new ActionStepDefinition(s3Inner1, "ValidateItem", integrationId, "item.validate",
//                 new Dictionary<string, TemplateOrLiteral>
//                 {
//                     ["item"] = TemplateOrLiteral.Literal("currentItem")
//                 },
//                 FailureStrategy.Skip,
//                 outputSchema: Schema(("valid", "bool"), ("sku", "string")), nextStepId: s3Inner2),

//             new ActionStepDefinition(s3Inner2, "CheckAvailability", integrationId, "inventory.check",
//                 new Dictionary<string, TemplateOrLiteral>
//                 {
//                     ["sku"] = TemplateOrLiteral.Literal("SKU-001")
//                 },
//                 FailureStrategy.Skip,
//                 outputSchema: Schema(("available", "bool"), ("quantity", "int")))
//         };

//         var steps = new List<StepDefinition>
//         {
//             new TriggerStepDefinition(s1, "PurchaseOrderReceived", integrationId, "erp.po.created",
//                 new Dictionary<string, string> { ["system"] = "SAP" }, s2,
//                 Schema(("poId", "string"), ("vendorId", "string"), ("lineItems", "array"), ("priority", "string"))),

//             new ActionStepDefinition(s2, "ReceivePO", integrationId, "po.receive",
//                 new Dictionary<string, TemplateOrLiteral>
//                 {
//                     ["poId"] = TemplateOrLiteral.Template("{{PurchaseOrderReceived.poId}}"),
//                     ["vendorId"] = TemplateOrLiteral.Template("{{PurchaseOrderReceived.vendorId}}")
//                 },
//                 FailureStrategy.Stop,
//                 outputSchema: Schema(("receivedAt", "string"), ("totalAmount", "decimal")), nextStepId: s3),

//             new LoopStepDefinition(s3, "ProcessLineItems",
//                 new TemplateReference("{{PurchaseOrderReceived.lineItems}}"),
//                 Schema(("currentItem", "string")),
//                 loopInnerSteps,
//                 ConcurrencyMode.Sequential,
//                 IterationFailureStrategy.Skip,
//                 retryCount: 0, nextStepId: s4),

//             new ParallelStepDefinition(s4, "CheckSuppliers",
//                 new List<StepId> { s5, s6 }, nextStepId: s7),

//             new ActionStepDefinition(s5, "CheckSupplierA", integrationId, "supplier.quote",
//                 new Dictionary<string, TemplateOrLiteral>
//                 {
//                     ["poId"] = TemplateOrLiteral.Template("{{PurchaseOrderReceived.poId}}"),
//                     ["supplier"] = TemplateOrLiteral.Literal("SupplierA")
//                 },
//                 FailureStrategy.Skip,
//                 outputSchema: Schema(("priceA", "decimal"), ("leadTimeA", "int"))),

//             new ActionStepDefinition(s6, "CheckSupplierB", integrationId, "supplier.quote",
//                 new Dictionary<string, TemplateOrLiteral>
//                 {
//                     ["poId"] = TemplateOrLiteral.Template("{{PurchaseOrderReceived.poId}}"),
//                     ["supplier"] = TemplateOrLiteral.Literal("SupplierB")
//                 },
//                 FailureStrategy.Skip,
//                 outputSchema: Schema(("priceB", "decimal"), ("leadTimeB", "int"))),

//             new ConditionStepDefinition(s7, "BestPrice",
//                 new List<ConditionRule>
//                 {
//                     new("{{CheckSupplierA.priceA}} <= {{CheckSupplierB.priceB}}", s8),
//                     new("{{CheckSupplierA.priceA}} > {{CheckSupplierB.priceB}}", s9)
//                 }, nextStepId: s10),

//             new ActionStepDefinition(s8, "OrderFromA", integrationId, "supplier.order",
//                 new Dictionary<string, TemplateOrLiteral>
//                 {
//                     ["poId"] = TemplateOrLiteral.Template("{{PurchaseOrderReceived.poId}}"),
//                     ["price"] = TemplateOrLiteral.Template("{{CheckSupplierA.priceA}}")
//                 },
//                 FailureStrategy.Retry, retryCount: 2,
//                 outputSchema: Schema(("orderId", "string"), ("confirmationRef", "string"))),

//             new ActionStepDefinition(s9, "OrderFromB", integrationId, "supplier.order",
//                 new Dictionary<string, TemplateOrLiteral>
//                 {
//                     ["poId"] = TemplateOrLiteral.Template("{{PurchaseOrderReceived.poId}}"),
//                     ["price"] = TemplateOrLiteral.Template("{{CheckSupplierB.priceB}}")
//                 },
//                 FailureStrategy.Retry, retryCount: 2,
//                 outputSchema: Schema(("orderId", "string"), ("confirmationRef", "string"))),

//             new ActionStepDefinition(s10, "ConsolidateOrders", integrationId, "order.consolidate",
//                 new Dictionary<string, TemplateOrLiteral>
//                 {
//                     ["poId"] = TemplateOrLiteral.Template("{{PurchaseOrderReceived.poId}}")
//                 },
//                 FailureStrategy.Stop,
//                 outputSchema: Schema(("consolidatedId", "string"), ("orderCount", "int")), nextStepId: s11),

//             new ConditionStepDefinition(s11, "ExpediteCheck",
//                 new List<ConditionRule>
//                 {
//                     new("{{PurchaseOrderReceived.priority}} == 'urgent'", s12),
//                     new("{{PurchaseOrderReceived.priority}} == 'normal'", s13)
//                 }, nextStepId: s14),

//             new ActionStepDefinition(s12, "ExpediteShipping", integrationId, "shipping.expedite",
//                 new Dictionary<string, TemplateOrLiteral>
//                 {
//                     ["consolidatedId"] = TemplateOrLiteral.Template("{{ConsolidateOrders.consolidatedId}}")
//                 },
//                 FailureStrategy.Stop,
//                 outputSchema: Schema(("shipmentId", "string"), ("eta", "string"))),

//             new ActionStepDefinition(s13, "StandardShipping", integrationId, "shipping.standard",
//                 new Dictionary<string, TemplateOrLiteral>
//                 {
//                     ["consolidatedId"] = TemplateOrLiteral.Template("{{ConsolidateOrders.consolidatedId}}")
//                 },
//                 FailureStrategy.Stop,
//                 outputSchema: Schema(("shipmentId", "string"), ("eta", "string"))),

//             new ActionStepDefinition(s14, "TrackShipment", integrationId, "shipping.track",
//                 new Dictionary<string, TemplateOrLiteral>
//                 {
//                     ["poId"] = TemplateOrLiteral.Template("{{PurchaseOrderReceived.poId}}")
//                 },
//                 FailureStrategy.Skip,
//                 outputSchema: Schema(("trackingId", "string"), ("location", "string")), nextStepId: s15),

//             new ActionStepDefinition(s15, "UpdateInventory", integrationId, "inventory.update",
//                 new Dictionary<string, TemplateOrLiteral>
//                 {
//                     ["poId"] = TemplateOrLiteral.Template("{{PurchaseOrderReceived.poId}}"),
//                     ["totalAmount"] = TemplateOrLiteral.Template("{{ReceivePO.totalAmount}}")
//                 },
//                 FailureStrategy.Stop,
//                 outputSchema: Schema(("inventoryId", "string"), ("updatedAt", "string")), nextStepId: s16),

//             new ActionStepDefinition(s16, "GeneratePOReport", integrationId, "report.po",
//                 new Dictionary<string, TemplateOrLiteral>
//                 {
//                     ["poId"] = TemplateOrLiteral.Template("{{PurchaseOrderReceived.poId}}")
//                 },
//                 FailureStrategy.Skip,
//                 outputSchema: Schema(("reportId", "string"), ("reportUrl", "string")), nextStepId: s17),

//             new ActionStepDefinition(s17, "NotifyProcurement", integrationId, "email.notify",
//                 new Dictionary<string, TemplateOrLiteral>
//                 {
//                     ["poId"] = TemplateOrLiteral.Template("{{PurchaseOrderReceived.poId}}"),
//                     ["reportUrl"] = TemplateOrLiteral.Template("{{GeneratePOReport.reportUrl}}")
//                 },
//                 FailureStrategy.Skip,
//                 outputSchema: Schema(("emailId", "string"), ("sentAt", "string")), nextStepId: s18),

//             new ActionStepDefinition(s18, "ReconcileInvoice", integrationId, "invoice.reconcile",
//                 new Dictionary<string, TemplateOrLiteral>
//                 {
//                     ["poId"] = TemplateOrLiteral.Template("{{PurchaseOrderReceived.poId}}"),
//                     ["totalAmount"] = TemplateOrLiteral.Template("{{ReceivePO.totalAmount}}")
//                 },
//                 FailureStrategy.Stop,
//                 outputSchema: Schema(("invoiceId", "string"), ("matchStatus", "string")), nextStepId: s19),

//             new ActionStepDefinition(s19, "AuditTrail", integrationId, "audit.log",
//                 new Dictionary<string, TemplateOrLiteral>
//                 {
//                     ["poId"] = TemplateOrLiteral.Template("{{PurchaseOrderReceived.poId}}")
//                 },
//                 FailureStrategy.Skip,
//                 outputSchema: Schema(("auditId", "string"), ("logTimestamp", "string")), nextStepId: s20),

//             new ActionStepDefinition(s20, "ClosePO", integrationId, "po.close",
//                 new Dictionary<string, TemplateOrLiteral>
//                 {
//                     ["poId"] = TemplateOrLiteral.Template("{{PurchaseOrderReceived.poId}}")
//                 },
//                 FailureStrategy.Stop,
//                 outputSchema: Schema(("closedAt", "string"), ("finalStatus", "string")))
//         };

//         return (steps, new SupplyChainIds(integrationId, s1, s2, s3, s3Inner1, s3Inner2,
//             s4, s5, s6, s7, s8, s9, s10, s11, s12, s13, s14, s15, s16, s17, s18, s19, s20));
//     }

//     // ──────────────────────────────────────────────────────────────────────
//     // Error 1: Trigger step has null OutputSchema (§8.6 rule 10)
//     // ──────────────────────────────────────────────────────────────────────
//     [Fact]
//     public void SupplyChain_TriggerNullOutputSchema_Fails()
//     {
//         var (steps, ids) = BuildValidSupplyChainPipeline();

//         steps[0] = new TriggerStepDefinition(ids.S1, "PurchaseOrderReceived", ids.IntegrationId, "erp.po.created",
//             new Dictionary<string, string> { ["system"] = "SAP" }, ids.S2,
//             outputSchema: null);

//         var ex = Assert.Throws<InvalidOperationException>(() =>
//             new WorkflowDefinitionAggregate(NewVersionId(), NewWorkflowId(), steps));
//         Assert.Contains("must have a non-null OutputSchema", ex.Message);
//     }

//     // ──────────────────────────────────────────────────────────────────────
//     // Error 2: Trigger is not the first step (§8.6 rule 1)
//     // ──────────────────────────────────────────────────────────────────────
//     [Fact]
//     public void SupplyChain_TriggerNotFirstStep_Fails()
//     {
//         var (steps, ids) = BuildValidSupplyChainPipeline();

//         (steps[0], steps[1]) = (steps[1], steps[0]);

//         var ex = Assert.Throws<InvalidOperationException>(() =>
//             new WorkflowDefinitionAggregate(NewVersionId(), NewWorkflowId(), steps));
//         Assert.Contains("first step in a workflow definition must be a trigger step", ex.Message);
//     }

//     // ──────────────────────────────────────────────────────────────────────
//     // Error 3: Multiple trigger steps (§8.6 rule 1)
//     // ──────────────────────────────────────────────────────────────────────
//     [Fact]
//     public void SupplyChain_MultipleTriggerSteps_Fails()
//     {
//         var (steps, ids) = BuildValidSupplyChainPipeline();

//         steps.Add(new TriggerStepDefinition(NewStepId(), "ManualPO", ids.IntegrationId, "manual.po",
//             new Dictionary<string, string> { ["mode"] = "manual" }, ids.S2,
//             Schema(("poId", "string"), ("vendorId", "string"))));

//         var ex = Assert.Throws<InvalidOperationException>(() =>
//             new WorkflowDefinitionAggregate(NewVersionId(), NewWorkflowId(), steps));
//         Assert.Contains("exactly one trigger step", ex.Message);
//     }

//     // ──────────────────────────────────────────────────────────────────────
//     // Error 4: Action step has null OutputSchema (§8.6 rule 10)
//     // ──────────────────────────────────────────────────────────────────────
//     [Fact]
//     public void SupplyChain_ActionNullOutputSchema_Fails()
//     {
//         var (steps, ids) = BuildValidSupplyChainPipeline();

//         steps[1] = new ActionStepDefinition(ids.S2, "ReceivePO", ids.IntegrationId, "po.receive",
//             new Dictionary<string, TemplateOrLiteral>
//             {
//                 ["poId"] = TemplateOrLiteral.Template("{{PurchaseOrderReceived.poId}}"),
//                 ["vendorId"] = TemplateOrLiteral.Template("{{PurchaseOrderReceived.vendorId}}")
//             },
//             FailureStrategy.Stop,
//             outputSchema: null, nextStepId: ids.S3);

//         var ex = Assert.Throws<InvalidOperationException>(() =>
//             new WorkflowDefinitionAggregate(NewVersionId(), NewWorkflowId(), steps));
//         Assert.Contains("must have a non-null OutputSchema", ex.Message);
//     }

//     // ──────────────────────────────────────────────────────────────────────
//     // Error 5: NextStepId references a step not in the list (§8.6 rule 3)
//     // ──────────────────────────────────────────────────────────────────────
//     [Fact]
//     public void SupplyChain_NextStepIdReferencesNonExistentStep_Fails()
//     {
//         var (steps, ids) = BuildValidSupplyChainPipeline();
//         var phantomStepId = NewStepId();

//         steps[13] = new ActionStepDefinition(ids.S14, "TrackShipment", ids.IntegrationId, "shipping.track",
//             new Dictionary<string, TemplateOrLiteral>
//             {
//                 ["poId"] = TemplateOrLiteral.Template("{{PurchaseOrderReceived.poId}}")
//             },
//             FailureStrategy.Skip,
//             outputSchema: Schema(("trackingId", "string"), ("location", "string")), nextStepId: phantomStepId);

//         var ex = Assert.Throws<InvalidOperationException>(() =>
//             new WorkflowDefinitionAggregate(NewVersionId(), NewWorkflowId(), steps));
//         Assert.Contains("is referenced but does not exist in the workflow", ex.Message);
//     }

//     // ──────────────────────────────────────────────────────────────────────
//     // Error 6: Cycle detected — linear chain back-edge (§8.6 rule 2)
//     //          AuditTrail chains back to TrackShipment in the main chain
//     // ──────────────────────────────────────────────────────────────────────
//     [Fact]
//     public void SupplyChain_CycleDetected_Fails()
//     {
//         var (steps, ids) = BuildValidSupplyChainPipeline();

//         // Replace ClosePO (index 19, last step) to point back to TrackShipment (s14),
//         // creating a linear cycle after all steps are visited: ... → s20 → s14
//         steps[19] = new ActionStepDefinition(ids.S20, "ClosePO", ids.IntegrationId, "po.close",
//             new Dictionary<string, TemplateOrLiteral>
//             {
//                 ["poId"] = TemplateOrLiteral.Template("{{PurchaseOrderReceived.poId}}")
//             },
//             FailureStrategy.Stop,
//             outputSchema: Schema(("closedAt", "string"), ("finalStatus", "string")), nextStepId: ids.S14);

//         var ex = Assert.Throws<InvalidOperationException>(() =>
//             new WorkflowDefinitionAggregate(NewVersionId(), NewWorkflowId(), steps));
//         Assert.Contains("Cycle detected", ex.Message);
//     }

//     // ──────────────────────────────────────────────────────────────────────
//     // Error 7: Template references an unknown step name (§8.5.1)
//     // ──────────────────────────────────────────────────────────────────────
//     [Fact]
//     public void SupplyChain_TemplateReferencesUnknownStepName_Fails()
//     {
//         var (steps, ids) = BuildValidSupplyChainPipeline();

//         steps[14] = new ActionStepDefinition(ids.S15, "UpdateInventory", ids.IntegrationId, "inventory.update",
//             new Dictionary<string, TemplateOrLiteral>
//             {
//                 ["poId"] = TemplateOrLiteral.Template("{{PurchaseOrderReceived.poId}}"),
//                 ["totalAmount"] = TemplateOrLiteral.Template("{{ReceivePurchase.totalAmount}}")
//             },
//             FailureStrategy.Stop,
//             outputSchema: Schema(("inventoryId", "string"), ("updatedAt", "string")), nextStepId: ids.S16);

//         var ex = Assert.Throws<InvalidOperationException>(() =>
//             new WorkflowDefinitionAggregate(NewVersionId(), NewWorkflowId(), steps));
//         Assert.Contains("references unknown step 'ReceivePurchase'", ex.Message);
//     }

//     // ──────────────────────────────────────────────────────────────────────
//     // Error 8: Template forward reference (§8.5.1)
//     //          ReceivePO references AuditTrail which is downstream
//     // ──────────────────────────────────────────────────────────────────────
//     [Fact]
//     public void SupplyChain_TemplateForwardReference_Fails()
//     {
//         var (steps, ids) = BuildValidSupplyChainPipeline();

//         steps[1] = new ActionStepDefinition(ids.S2, "ReceivePO", ids.IntegrationId, "po.receive",
//             new Dictionary<string, TemplateOrLiteral>
//             {
//                 ["poId"] = TemplateOrLiteral.Template("{{PurchaseOrderReceived.poId}}"),
//                 ["auditId"] = TemplateOrLiteral.Template("{{AuditTrail.auditId}}")
//             },
//             FailureStrategy.Stop,
//             outputSchema: Schema(("receivedAt", "string"), ("totalAmount", "decimal")), nextStepId: ids.S3);

//         var ex = Assert.Throws<InvalidOperationException>(() =>
//             new WorkflowDefinitionAggregate(NewVersionId(), NewWorkflowId(), steps));
//         Assert.Contains("references step 'AuditTrail' before it is guaranteed to complete", ex.Message);
//     }

//     // ──────────────────────────────────────────────────────────────────────
//     // Error 9: Template references a non-existent field (§8.5.2)
//     // ──────────────────────────────────────────────────────────────────────
//     [Fact]
//     public void SupplyChain_TemplateReferencesNonExistentField_Fails()
//     {
//         var (steps, ids) = BuildValidSupplyChainPipeline();

//         steps[19] = new ActionStepDefinition(ids.S20, "ClosePO", ids.IntegrationId, "po.close",
//             new Dictionary<string, TemplateOrLiteral>
//             {
//                 ["poId"] = TemplateOrLiteral.Template("{{PurchaseOrderReceived.orderId}}")
//             },
//             FailureStrategy.Stop,
//             outputSchema: Schema(("closedAt", "string"), ("finalStatus", "string")));

//         var ex = Assert.Throws<InvalidOperationException>(() =>
//             new WorkflowDefinitionAggregate(NewVersionId(), NewWorkflowId(), steps));
//         Assert.Contains("references non-existent field 'orderId' on step 'PurchaseOrderReceived'", ex.Message);
//     }

//     // ──────────────────────────────────────────────────────────────────────
//     // Error 10: Orphaned unreachable steps (§8.6 rule 4)
//     //           Per requirements, even 1 unreachable step must be detected
//     // ──────────────────────────────────────────────────────────────────────
//     [Fact]
//     public void SupplyChain_OrphanedUnreachableSteps_Fails()
//     {
//         var (steps, ids) = BuildValidSupplyChainPipeline();

//         steps.Add(new ActionStepDefinition(NewStepId(), "DanglingStep", ids.IntegrationId, "dangling.action",
//             new Dictionary<string, TemplateOrLiteral> { ["input"] = TemplateOrLiteral.Literal("data") },
//             FailureStrategy.Stop,
//             outputSchema: Schema(("result", "string"), ("status", "string"))));

//         var ex = Assert.Throws<InvalidOperationException>(() =>
//             new WorkflowDefinitionAggregate(NewVersionId(), NewWorkflowId(), steps));
//         Assert.Contains("unreachable (orphaned) steps", ex.Message);
//     }

//     // ──────────────────────────────────────────────────────────────────────
//     // Error 11: Condition branch last step has NextStepId != null (§8.6 rule 6)
//     //           "The last step in every condition branch must have NextStepId = null"
//     // ──────────────────────────────────────────────────────────────────────
//     [Fact]
//     public void SupplyChain_BranchLastStepNextStepIdNotNull_Fails()
//     {
//         var (steps, ids) = BuildValidSupplyChainPipeline();

//         // OrderFromB (s9, index 8) is the last step of the "supplierB cheaper" condition branch.
//         steps[8] = new ActionStepDefinition(ids.S9, "OrderFromB", ids.IntegrationId, "supplier.order",
//             new Dictionary<string, TemplateOrLiteral>
//             {
//                 ["poId"] = TemplateOrLiteral.Template("{{PurchaseOrderReceived.poId}}"),
//                 ["price"] = TemplateOrLiteral.Template("{{CheckSupplierB.priceB}}")
//             },
//             FailureStrategy.Retry, retryCount: 2,
//             outputSchema: Schema(("orderId", "string"), ("confirmationRef", "string")),
//             nextStepId: ids.S10);

//         var ex = Assert.Throws<InvalidOperationException>(() =>
//             new WorkflowDefinitionAggregate(NewVersionId(), NewWorkflowId(), steps));
//         Assert.Contains("NextStepId", ex.Message);
//     }

//     // ──────────────────────────────────────────────────────────────────────
//     // Error 12: Condition step with zero rules (§8.6 rule 7)
//     //           "A ConditionStepDefinition must have at least one ConditionRule"
//     // ──────────────────────────────────────────────────────────────────────
//     [Fact]
//     public void SupplyChain_ConditionWithZeroRules_Fails()
//     {
//         var (steps, ids) = BuildValidSupplyChainPipeline();

//         steps[6] = new ConditionStepDefinition(ids.S7, "BestPrice",
//             new List<ConditionRule>(), nextStepId: ids.S10);

//         var ex = Assert.Throws<InvalidOperationException>(() =>
//             new WorkflowDefinitionAggregate(NewVersionId(), NewWorkflowId(), steps));
//         Assert.Contains("rule", ex.Message, StringComparison.OrdinalIgnoreCase);
//     }
// }

// /// <summary>
// /// 12 validation failure tests for the Content Moderation pipeline (Scenario 10).
// /// 18-step pipeline with parallel AI analysis (3 branches), loop (ReviewFlags, 2 inner steps),
// /// two conditions (AutoDecision and SeverityCheck), and template references.
// /// Tests are derived from requirements §8.5 and §8.6, not from implementation.
// /// </summary>
// public class ContentModerationValidationFailureTests
// {
//     private static StepId NewStepId() => new(Guid.NewGuid());
//     private static IntegrationId NewIntegrationId() => new(Guid.NewGuid());
//     private static WorkflowVersionId NewVersionId() => new(Guid.NewGuid());
//     private static WorkflowId NewWorkflowId() => new(Guid.NewGuid());

//     private static StepOutputSchema Schema(params (string key, string type)[] fields)
//     {
//         var dict = new Dictionary<string, string>();
//         foreach (var (key, type) in fields)
//             dict[key] = type;
//         return new StepOutputSchema(dict);
//     }

//     private record ContentModerationIds(
//         IntegrationId IntegrationId,
//         StepId S1, StepId S2, StepId S3, StepId S4, StepId S5, StepId S6,
//         StepId S7, StepId S8, StepId S9, StepId S10, StepId S10Inner1, StepId S10Inner2,
//         StepId S11, StepId S12, StepId S13, StepId S14, StepId S15, StepId S16,
//         StepId S17, StepId S18);

//     private static (List<StepDefinition> steps, ContentModerationIds ids) BuildValidContentModerationPipeline()
//     {
//         var integrationId = NewIntegrationId();
//         var s1 = NewStepId();
//         var s2 = NewStepId();
//         var s3 = NewStepId();
//         var s4 = NewStepId();
//         var s5 = NewStepId();
//         var s6 = NewStepId();
//         var s7 = NewStepId();
//         var s8 = NewStepId();
//         var s9 = NewStepId();
//         var s10 = NewStepId();
//         var s10Inner1 = NewStepId();
//         var s10Inner2 = NewStepId();
//         var s11 = NewStepId();
//         var s12 = NewStepId();
//         var s13 = NewStepId();
//         var s14 = NewStepId();
//         var s15 = NewStepId();
//         var s16 = NewStepId();
//         var s17 = NewStepId();
//         var s18 = NewStepId();

//         var loopInnerSteps = new List<StepDefinition>
//         {
//             new ActionStepDefinition(s10Inner1, "EvaluateFlag", integrationId, "moderation.evaluate",
//                 new Dictionary<string, TemplateOrLiteral>
//                 {
//                     ["flag"] = TemplateOrLiteral.Literal("currentFlag")
//                 },
//                 FailureStrategy.Skip,
//                 outputSchema: Schema(("flagType", "string"), ("confidence", "decimal")), nextStepId: s10Inner2),

//             new ActionStepDefinition(s10Inner2, "RecordVerdict", integrationId, "moderation.record",
//                 new Dictionary<string, TemplateOrLiteral>
//                 {
//                     ["flagType"] = TemplateOrLiteral.Literal("spam")
//                 },
//                 FailureStrategy.Skip,
//                 outputSchema: Schema(("verdictId", "string"), ("action", "string")))
//         };

//         var steps = new List<StepDefinition>
//         {
//             new TriggerStepDefinition(s1, "ContentSubmitted", integrationId, "content.submitted",
//                 new Dictionary<string, string> { ["platform"] = "social" }, s2,
//                 Schema(("contentId", "string"), ("userId", "string"), ("contentType", "string"), ("flags", "array"))),

//             new ActionStepDefinition(s2, "IngestContent", integrationId, "content.ingest",
//                 new Dictionary<string, TemplateOrLiteral>
//                 {
//                     ["contentId"] = TemplateOrLiteral.Template("{{ContentSubmitted.contentId}}"),
//                     ["contentType"] = TemplateOrLiteral.Template("{{ContentSubmitted.contentType}}")
//                 },
//                 FailureStrategy.Stop,
//                 outputSchema: Schema(("ingestId", "string"), ("size", "int")), nextStepId: s3),

//             new ParallelStepDefinition(s3, "AIAnalysis",
//                 new List<StepId> { s4, s5, s6 }, nextStepId: s7),

//             new ActionStepDefinition(s4, "TextAnalysis", integrationId, "ai.text",
//                 new Dictionary<string, TemplateOrLiteral>
//                 {
//                     ["contentId"] = TemplateOrLiteral.Template("{{ContentSubmitted.contentId}}")
//                 },
//                 FailureStrategy.Skip,
//                 outputSchema: Schema(("toxicityScore", "decimal"), ("sentiment", "string"))),

//             new ActionStepDefinition(s5, "ImageAnalysis", integrationId, "ai.image",
//                 new Dictionary<string, TemplateOrLiteral>
//                 {
//                     ["contentId"] = TemplateOrLiteral.Template("{{ContentSubmitted.contentId}}")
//                 },
//                 FailureStrategy.Skip,
//                 outputSchema: Schema(("nsfwScore", "decimal"), ("objectDetection", "string"))),

//             new ActionStepDefinition(s6, "VideoAnalysis", integrationId, "ai.video",
//                 new Dictionary<string, TemplateOrLiteral>
//                 {
//                     ["contentId"] = TemplateOrLiteral.Template("{{ContentSubmitted.contentId}}")
//                 },
//                 FailureStrategy.Skip,
//                 outputSchema: Schema(("violenceScore", "decimal"), ("duration", "int"))),

//             new ActionStepDefinition(s7, "AggregateScores", integrationId, "moderation.aggregate",
//                 new Dictionary<string, TemplateOrLiteral>
//                 {
//                     ["toxicity"] = TemplateOrLiteral.Template("{{TextAnalysis.toxicityScore}}"),
//                     ["nsfw"] = TemplateOrLiteral.Template("{{ImageAnalysis.nsfwScore}}"),
//                     ["violence"] = TemplateOrLiteral.Template("{{VideoAnalysis.violenceScore}}")
//                 },
//                 FailureStrategy.Stop,
//                 outputSchema: Schema(("overallScore", "decimal"), ("decision", "string"), ("severity", "string")), nextStepId: s8),

//             new ConditionStepDefinition(s8, "AutoDecision",
//                 new List<ConditionRule>
//                 {
//                     new("{{AggregateScores.decision}} == 'safe'", s9),
//                     new("{{AggregateScores.decision}} == 'flagged'", s10)
//                 }, nextStepId: s15),

//             new ActionStepDefinition(s9, "Publish", integrationId, "content.publish",
//                 new Dictionary<string, TemplateOrLiteral>
//                 {
//                     ["contentId"] = TemplateOrLiteral.Template("{{ContentSubmitted.contentId}}")
//                 },
//                 FailureStrategy.Stop,
//                 outputSchema: Schema(("publishedAt", "string"), ("url", "string"))),

//             new LoopStepDefinition(s10, "ReviewFlags",
//                 new TemplateReference("{{ContentSubmitted.flags}}"),
//                 Schema(("currentFlag", "string")),
//                 loopInnerSteps,
//                 ConcurrencyMode.Sequential,
//                 IterationFailureStrategy.Skip,
//                 retryCount: 0, nextStepId: s11),

//             new ConditionStepDefinition(s11, "SeverityCheck",
//                 new List<ConditionRule>
//                 {
//                     new("{{AggregateScores.severity}} == 'high'", s12),
//                     new("{{AggregateScores.severity}} == 'medium'", s14)
//                 }),

//             new ActionStepDefinition(s12, "RemoveContent", integrationId, "content.remove",
//                 new Dictionary<string, TemplateOrLiteral>
//                 {
//                     ["contentId"] = TemplateOrLiteral.Template("{{ContentSubmitted.contentId}}")
//                 },
//                 FailureStrategy.Stop,
//                 outputSchema: Schema(("removedAt", "string"), ("reason", "string")), nextStepId: s13),

//             new ActionStepDefinition(s13, "SuspendUser", integrationId, "user.suspend",
//                 new Dictionary<string, TemplateOrLiteral>
//                 {
//                     ["userId"] = TemplateOrLiteral.Template("{{ContentSubmitted.userId}}")
//                 },
//                 FailureStrategy.Stop,
//                 outputSchema: Schema(("suspendedAt", "string"), ("duration", "string"))),

//             new ActionStepDefinition(s14, "HideContent", integrationId, "content.hide",
//                 new Dictionary<string, TemplateOrLiteral>
//                 {
//                     ["contentId"] = TemplateOrLiteral.Template("{{ContentSubmitted.contentId}}")
//                 },
//                 FailureStrategy.Stop,
//                 outputSchema: Schema(("hiddenAt", "string"), ("reviewRequired", "bool"))),

//             new ActionStepDefinition(s15, "LogDecision", integrationId, "audit.logDecision",
//                 new Dictionary<string, TemplateOrLiteral>
//                 {
//                     ["contentId"] = TemplateOrLiteral.Template("{{ContentSubmitted.contentId}}"),
//                     ["overallScore"] = TemplateOrLiteral.Template("{{AggregateScores.overallScore}}")
//                 },
//                 FailureStrategy.Skip,
//                 outputSchema: Schema(("logId", "string"), ("loggedAt", "string")), nextStepId: s16),

//             new ActionStepDefinition(s16, "NotifyCreator", integrationId, "notification.send",
//                 new Dictionary<string, TemplateOrLiteral>
//                 {
//                     ["userId"] = TemplateOrLiteral.Template("{{ContentSubmitted.userId}}"),
//                     ["decision"] = TemplateOrLiteral.Template("{{AggregateScores.decision}}")
//                 },
//                 FailureStrategy.Skip,
//                 outputSchema: Schema(("notificationId", "string"), ("sentAt", "string")), nextStepId: s17),

//             new ActionStepDefinition(s17, "GenerateReport", integrationId, "report.moderation",
//                 new Dictionary<string, TemplateOrLiteral>
//                 {
//                     ["contentId"] = TemplateOrLiteral.Template("{{ContentSubmitted.contentId}}")
//                 },
//                 FailureStrategy.Skip,
//                 outputSchema: Schema(("reportId", "string"), ("reportUrl", "string")), nextStepId: s18),

//             new ActionStepDefinition(s18, "ArchiveAudit", integrationId, "audit.archive",
//                 new Dictionary<string, TemplateOrLiteral>
//                 {
//                     ["contentId"] = TemplateOrLiteral.Template("{{ContentSubmitted.contentId}}")
//                 },
//                 FailureStrategy.Stop,
//                 outputSchema: Schema(("archiveId", "string"), ("archivedAt", "string")))
//         };

//         return (steps, new ContentModerationIds(integrationId, s1, s2, s3, s4, s5, s6,
//             s7, s8, s9, s10, s10Inner1, s10Inner2, s11, s12, s13, s14, s15, s16, s17, s18));
//     }

//     // ──────────────────────────────────────────────────────────────────────
//     // Error 1: Trigger step has null OutputSchema (§8.6 rule 10)
//     // ──────────────────────────────────────────────────────────────────────
//     [Fact]
//     public void ContentModeration_TriggerNullOutputSchema_Fails()
//     {
//         var (steps, ids) = BuildValidContentModerationPipeline();

//         steps[0] = new TriggerStepDefinition(ids.S1, "ContentSubmitted", ids.IntegrationId, "content.submitted",
//             new Dictionary<string, string> { ["platform"] = "social" }, ids.S2,
//             outputSchema: null);

//         var ex = Assert.Throws<InvalidOperationException>(() =>
//             new WorkflowDefinitionAggregate(NewVersionId(), NewWorkflowId(), steps));
//         Assert.Contains("must have a non-null OutputSchema", ex.Message);
//     }

//     // ──────────────────────────────────────────────────────────────────────
//     // Error 2: Trigger is not the first step (§8.6 rule 1)
//     // ──────────────────────────────────────────────────────────────────────
//     [Fact]
//     public void ContentModeration_TriggerNotFirstStep_Fails()
//     {
//         var (steps, ids) = BuildValidContentModerationPipeline();

//         (steps[0], steps[1]) = (steps[1], steps[0]);

//         var ex = Assert.Throws<InvalidOperationException>(() =>
//             new WorkflowDefinitionAggregate(NewVersionId(), NewWorkflowId(), steps));
//         Assert.Contains("first step in a workflow definition must be a trigger step", ex.Message);
//     }

//     // ──────────────────────────────────────────────────────────────────────
//     // Error 3: Multiple trigger steps (§8.6 rule 1)
//     // ──────────────────────────────────────────────────────────────────────
//     [Fact]
//     public void ContentModeration_MultipleTriggerSteps_Fails()
//     {
//         var (steps, ids) = BuildValidContentModerationPipeline();

//         steps.Add(new TriggerStepDefinition(NewStepId(), "ManualSubmit", ids.IntegrationId, "manual.submit",
//             new Dictionary<string, string> { ["mode"] = "manual" }, ids.S2,
//             Schema(("contentId", "string"), ("userId", "string"))));

//         var ex = Assert.Throws<InvalidOperationException>(() =>
//             new WorkflowDefinitionAggregate(NewVersionId(), NewWorkflowId(), steps));
//         Assert.Contains("exactly one trigger step", ex.Message);
//     }

//     // ──────────────────────────────────────────────────────────────────────
//     // Error 4: Action step has null OutputSchema (§8.6 rule 10)
//     // ──────────────────────────────────────────────────────────────────────
//     [Fact]
//     public void ContentModeration_ActionNullOutputSchema_Fails()
//     {
//         var (steps, ids) = BuildValidContentModerationPipeline();

//         steps[1] = new ActionStepDefinition(ids.S2, "IngestContent", ids.IntegrationId, "content.ingest",
//             new Dictionary<string, TemplateOrLiteral>
//             {
//                 ["contentId"] = TemplateOrLiteral.Template("{{ContentSubmitted.contentId}}"),
//                 ["contentType"] = TemplateOrLiteral.Template("{{ContentSubmitted.contentType}}")
//             },
//             FailureStrategy.Stop,
//             outputSchema: null, nextStepId: ids.S3);

//         var ex = Assert.Throws<InvalidOperationException>(() =>
//             new WorkflowDefinitionAggregate(NewVersionId(), NewWorkflowId(), steps));
//         Assert.Contains("must have a non-null OutputSchema", ex.Message);
//     }

//     // ──────────────────────────────────────────────────────────────────────
//     // Error 5: NextStepId references a step not in the list (§8.6 rule 3)
//     // ──────────────────────────────────────────────────────────────────────
//     [Fact]
//     public void ContentModeration_NextStepIdReferencesNonExistentStep_Fails()
//     {
//         var (steps, ids) = BuildValidContentModerationPipeline();
//         var phantomStepId = NewStepId();

//         steps[14] = new ActionStepDefinition(ids.S15, "LogDecision", ids.IntegrationId, "audit.logDecision",
//             new Dictionary<string, TemplateOrLiteral>
//             {
//                 ["contentId"] = TemplateOrLiteral.Template("{{ContentSubmitted.contentId}}"),
//                 ["overallScore"] = TemplateOrLiteral.Template("{{AggregateScores.overallScore}}")
//             },
//             FailureStrategy.Skip,
//             outputSchema: Schema(("logId", "string"), ("loggedAt", "string")), nextStepId: phantomStepId);

//         var ex = Assert.Throws<InvalidOperationException>(() =>
//             new WorkflowDefinitionAggregate(NewVersionId(), NewWorkflowId(), steps));
//         Assert.Contains("is referenced but does not exist in the workflow", ex.Message);
//     }

//     // ──────────────────────────────────────────────────────────────────────
//     // Error 6: Cycle detected — linear chain back-edge (§8.6 rule 2)
//     //          GenerateReport chains back to LogDecision in the main chain
//     // ──────────────────────────────────────────────────────────────────────
//     [Fact]
//     public void ContentModeration_CycleDetected_Fails()
//     {
//         var (steps, ids) = BuildValidContentModerationPipeline();

//         // Replace ArchiveAudit (index 17, last step) to point back to LogDecision (s15),
//         // creating a linear cycle after all steps are visited: ... → s18 → s15
//         steps[17] = new ActionStepDefinition(ids.S18, "ArchiveAudit", ids.IntegrationId, "audit.archive",
//             new Dictionary<string, TemplateOrLiteral>
//             {
//                 ["contentId"] = TemplateOrLiteral.Template("{{ContentSubmitted.contentId}}")
//             },
//             FailureStrategy.Stop,
//             outputSchema: Schema(("archiveId", "string"), ("archivedAt", "string")), nextStepId: ids.S15);

//         var ex = Assert.Throws<InvalidOperationException>(() =>
//             new WorkflowDefinitionAggregate(NewVersionId(), NewWorkflowId(), steps));
//         Assert.Contains("Cycle detected", ex.Message);
//     }

//     // ──────────────────────────────────────────────────────────────────────
//     // Error 7: Template references an unknown step name (§8.5.1)
//     // ──────────────────────────────────────────────────────────────────────
//     [Fact]
//     public void ContentModeration_TemplateReferencesUnknownStepName_Fails()
//     {
//         var (steps, ids) = BuildValidContentModerationPipeline();

//         steps[6] = new ActionStepDefinition(ids.S7, "AggregateScores", ids.IntegrationId, "moderation.aggregate",
//             new Dictionary<string, TemplateOrLiteral>
//             {
//                 ["toxicity"] = TemplateOrLiteral.Template("{{TextNLP.toxicityScore}}"),
//                 ["nsfw"] = TemplateOrLiteral.Template("{{ImageAnalysis.nsfwScore}}"),
//                 ["violence"] = TemplateOrLiteral.Template("{{VideoAnalysis.violenceScore}}")
//             },
//             FailureStrategy.Stop,
//             outputSchema: Schema(("overallScore", "decimal"), ("decision", "string"), ("severity", "string")), nextStepId: ids.S8);

//         var ex = Assert.Throws<InvalidOperationException>(() =>
//             new WorkflowDefinitionAggregate(NewVersionId(), NewWorkflowId(), steps));
//         Assert.Contains("references unknown step 'TextNLP'", ex.Message);
//     }

//     // ──────────────────────────────────────────────────────────────────────
//     // Error 8: Template forward reference (§8.5.1)
//     //          IngestContent references GenerateReport which is downstream
//     // ──────────────────────────────────────────────────────────────────────
//     [Fact]
//     public void ContentModeration_TemplateForwardReference_Fails()
//     {
//         var (steps, ids) = BuildValidContentModerationPipeline();

//         steps[1] = new ActionStepDefinition(ids.S2, "IngestContent", ids.IntegrationId, "content.ingest",
//             new Dictionary<string, TemplateOrLiteral>
//             {
//                 ["contentId"] = TemplateOrLiteral.Template("{{ContentSubmitted.contentId}}"),
//                 ["reportId"] = TemplateOrLiteral.Template("{{GenerateReport.reportId}}")
//             },
//             FailureStrategy.Stop,
//             outputSchema: Schema(("ingestId", "string"), ("size", "int")), nextStepId: ids.S3);

//         var ex = Assert.Throws<InvalidOperationException>(() =>
//             new WorkflowDefinitionAggregate(NewVersionId(), NewWorkflowId(), steps));
//         Assert.Contains("references step 'GenerateReport' before it is guaranteed to complete", ex.Message);
//     }

//     // ──────────────────────────────────────────────────────────────────────
//     // Error 9: Template references a non-existent field (§8.5.2)
//     // ──────────────────────────────────────────────────────────────────────
//     [Fact]
//     public void ContentModeration_TemplateReferencesNonExistentField_Fails()
//     {
//         var (steps, ids) = BuildValidContentModerationPipeline();

//         steps[17] = new ActionStepDefinition(ids.S18, "ArchiveAudit", ids.IntegrationId, "audit.archive",
//             new Dictionary<string, TemplateOrLiteral>
//             {
//                 ["contentId"] = TemplateOrLiteral.Template("{{ContentSubmitted.mediaType}}")
//             },
//             FailureStrategy.Stop,
//             outputSchema: Schema(("archiveId", "string"), ("archivedAt", "string")));

//         var ex = Assert.Throws<InvalidOperationException>(() =>
//             new WorkflowDefinitionAggregate(NewVersionId(), NewWorkflowId(), steps));
//         Assert.Contains("references non-existent field 'mediaType' on step 'ContentSubmitted'", ex.Message);
//     }

//     // ──────────────────────────────────────────────────────────────────────
//     // Error 10: Orphaned unreachable steps (§8.6 rule 4)
//     //           Per requirements, even 1 unreachable step must be detected
//     // ──────────────────────────────────────────────────────────────────────
//     [Fact]
//     public void ContentModeration_OrphanedUnreachableSteps_Fails()
//     {
//         var (steps, ids) = BuildValidContentModerationPipeline();

//         steps.Add(new ActionStepDefinition(NewStepId(), "DanglingStep", ids.IntegrationId, "dangling.action",
//             new Dictionary<string, TemplateOrLiteral> { ["input"] = TemplateOrLiteral.Literal("data") },
//             FailureStrategy.Stop,
//             outputSchema: Schema(("result", "string"), ("status", "string"))));

//         var ex = Assert.Throws<InvalidOperationException>(() =>
//             new WorkflowDefinitionAggregate(NewVersionId(), NewWorkflowId(), steps));
//         Assert.Contains("unreachable (orphaned) steps", ex.Message);
//     }

//     // ──────────────────────────────────────────────────────────────────────
//     // Error 11: Condition branch last step has NextStepId != null (§8.6 rule 6)
//     //           "The last step in every condition branch must have NextStepId = null"
//     // ──────────────────────────────────────────────────────────────────────
//     [Fact]
//     public void ContentModeration_BranchLastStepNextStepIdNotNull_Fails()
//     {
//         var (steps, ids) = BuildValidContentModerationPipeline();

//         // HideContent (s14, index 13) is the last step of the "medium severity" condition branch.
//         steps[13] = new ActionStepDefinition(ids.S14, "HideContent", ids.IntegrationId, "content.hide",
//             new Dictionary<string, TemplateOrLiteral>
//             {
//                 ["contentId"] = TemplateOrLiteral.Template("{{ContentSubmitted.contentId}}")
//             },
//             FailureStrategy.Stop,
//             outputSchema: Schema(("hiddenAt", "string"), ("reviewRequired", "bool")),
//             nextStepId: ids.S15);

//         var ex = Assert.Throws<InvalidOperationException>(() =>
//             new WorkflowDefinitionAggregate(NewVersionId(), NewWorkflowId(), steps));
//         Assert.Contains("NextStepId", ex.Message);
//     }

//     // ──────────────────────────────────────────────────────────────────────
//     // Error 12: Condition step with zero rules (§8.6 rule 7)
//     //           "A ConditionStepDefinition must have at least one ConditionRule"
//     // ──────────────────────────────────────────────────────────────────────
//     [Fact]
//     public void ContentModeration_ConditionWithZeroRules_Fails()
//     {
//         var (steps, ids) = BuildValidContentModerationPipeline();

//         steps[7] = new ConditionStepDefinition(ids.S8, "AutoDecision",
//             new List<ConditionRule>(), nextStepId: ids.S15);

//         var ex = Assert.Throws<InvalidOperationException>(() =>
//             new WorkflowDefinitionAggregate(NewVersionId(), NewWorkflowId(), steps));
//         Assert.Contains("rule", ex.Message, StringComparison.OrdinalIgnoreCase);
//     }
// }
