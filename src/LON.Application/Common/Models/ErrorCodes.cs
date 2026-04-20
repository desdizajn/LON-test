namespace LON.Application.Common.Models;

/// <summary>
/// Stable, snake_case error codes surfaced to the UI. The client looks each
/// one up in its i18n dictionary under the `errors.*` namespace and falls back
/// to <see cref="Result.ErrorMessage"/> when the code is absent.
/// </summary>
public static class ErrorCodes
{
    // Receipts
    public const string ReceiptEmptyLines = "receipt.empty_lines";
    public const string ReceiptNoLocation = "receipt.no_location";

    // MRN registry probes (receipt + customs consumption)
    public const string MrnNotRegistered = "mrn.not_registered";
    public const string MrnDeactivated = "mrn.deactivated";
    public const string MrnExpired = "mrn.expired";
    public const string MrnOverdraw = "mrn.overdraw";
    public const string MrnDuplicate = "mrn.duplicate";

    // Customs declarations
    public const string DeclarationEmptyLines = "declaration.empty_lines";
    public const string ProcedureNotFound = "procedure.not_found";
    public const string ProcedureInactive = "procedure.inactive";
    public const string AuthorizationNotFound = "authorization.not_found";
    public const string AuthorizationInactive = "authorization.inactive";
    public const string AuthorizationExpired = "authorization.expired";
    public const string AuthorizationNotIssued = "authorization.not_issued";

    // Export / Return / Waste
    public const string ExportEmptyLines = "export.empty_lines";
    public const string ExportMrnRequired = "export.mrn_required";
    public const string ExportMrnNotFound = "export.mrn_not_found";
    public const string ExportDischargeInvalid = "export.discharge_invalid";
    public const string ExportOverDischarge = "export.over_discharge";
    public const string ReturnEmptyLines = "return.empty_lines";
    public const string ReturnMrnRequired = "return.mrn_required";
    public const string ReturnMrnNotFound = "return.mrn_not_found";
    public const string ReturnQuantityInvalid = "return.quantity_invalid";
    public const string ReturnOver = "return.over";
    public const string WasteQuantityInvalid = "waste.quantity_invalid";
    public const string WasteReasonRequired = "waste.reason_required";
    public const string WasteMrnRequired = "waste.mrn_required";
    public const string WasteMrnNotRegistered = "waste.mrn_not_registered";
    public const string WasteOverPool = "waste.over_pool";
    public const string WasteSlotsMax = "waste.slots_max";
    public const string WasteSlotIndexInvalid = "waste.slot_index_invalid";
    public const string WasteSlotQuantityInvalid = "waste.slot_quantity_invalid";
    public const string WasteSlotDuplicateIndex = "waste.slot_duplicate_index";
    public const string WasteSlotSumMismatch = "waste.slot_sum_mismatch";

    // Certify (Zaverka)
    public const string CertifyZaverkaRequired = "certify.zaverka_required";
    public const string CertifyDeclarationNotFound = "certify.declaration_not_found";
    public const string CertifyAlreadyCertified = "certify.already_certified";
    public const string CertifyRemovedDeclaration = "certify.removed_declaration";
    public const string CertifyZaverkaDuplicate = "certify.zaverka_duplicate";

    // Production
    public const string PoNotFound = "po.not_found";
    public const string PoInvalidStatus = "po.invalid_status";
    public const string BomNotFound = "bom.not_found";
    public const string BomBaseQuantityInvalid = "bom.base_quantity_invalid";
    public const string ProductionReceiptQuantityInvalid = "production_receipt.quantity_invalid";
    public const string ProductionReceiptScrapInvalid = "production_receipt.scrap_invalid";
    public const string ProductionReceiptBatchRequired = "production_receipt.batch_required";
    public const string IssueAllInvalidStatus = "issue_all.invalid_status";
    public const string IssueAllNoMaterials = "issue_all.no_materials";
    public const string IssueAllAllIssued = "issue_all.all_issued";
    public const string MaterialIssueEmptyLines = "material_issue.empty_lines";
    public const string MaterialIssuePoNotFound = "material_issue.po_not_found";
    public const string MaterialIssueQuantityInvalid = "material_issue.quantity_invalid";
    public const string MaterialIssueInsufficientInventory = "material_issue.insufficient_inventory";
    public const string MaterialIssueLonMissingBatchMrn = "material_issue.lon_missing_batch_mrn";

    // WMS bulk ops
    public const string BatchNotFound = "batch.not_found";
    public const string BatchNoMovementNeeded = "batch.no_movement_needed";
    public const string LocationNotFound = "location.not_found";
    public const string TransferNoFilter = "transfer.no_filter";
    public const string TransferNoMatch = "transfer.no_match";

    // FEFO policy flag
    public const string FefoDisabled = "fefo.disabled";

    // Users
    public const string UserUsernameRequired = "user.username_required";
    public const string UserEmailRequired = "user.email_required";
    public const string UserPasswordRequired = "user.password_required";
    public const string UserUsernameTaken = "user.username_taken";
    public const string UserTenantNotFound = "user.tenant_not_found";
    public const string UserRolesInvalid = "user.roles_invalid";

    // Import
    public const string ImportEmptyFile = "import.empty_file";
    public const string ImportFilenameRequired = "import.filename_required";
    public const string ImportParseError = "import.parse_error";
    public const string ImportNoHeaders = "import.no_headers";
    public const string ImportSessionNotFound = "import.session_not_found";
    public const string ImportNoTarget = "import.no_target";
    public const string ImportNoMapping = "import.no_mapping";
    public const string ImportAlreadyCommitted = "import.already_committed";
    public const string ImportNoExecutor = "import.no_executor";
    public const string ImportCommitFailed = "import.commit_failed";
    public const string MappingTargetRequired = "mapping.target_required";
    public const string MappingEmpty = "mapping.empty";
    public const string MappingUnknownSourceColumn = "mapping.unknown_source_column";
    public const string MappingUnmappedColumn = "mapping.unmapped_column";

    // Quick entry
    public const string QuickEntryEmptyCommand = "quick_entry.empty_command";
    public const string QuickEntryInvalidCommand = "quick_entry.invalid_command";
    public const string QuickEntryIssueUsage = "quick_entry.issue_usage";
    public const string QuickEntryReleaseUsage = "quick_entry.release_usage";
    public const string QuickEntryMoveUsage = "quick_entry.move_usage";
    public const string QuickEntryPoNotFound = "quick_entry.po_not_found";
    public const string QuickEntryUnknownStage = "quick_entry.unknown_stage";

    // Generic
    public const string ValidationFailed = "validation.failed";
    public const string InternalError = "internal.error";
}
