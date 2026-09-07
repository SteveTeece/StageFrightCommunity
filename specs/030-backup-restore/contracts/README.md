# Contracts — Restore From Backup During First-Run Setup

The stable surfaces this feature exposes, that consumers and tests code against. Identifiers here are pinned — implementation and tests must match them exactly (including the Verbatim Constraints from `spec.md`: the restore control is a **checkbox**, and the default backup filename contains the literal word **`backup`**).

| File | Covers |
|---|---|
| [`backup-service-contracts.md`](backup-service-contracts.md) | `IBackupService`, `IBackupRepository`, `IBackupDestinationPicker`, `IRecoveryCopyStore` (C# signatures); `BackupVerificationException`, `BackupVerificationResult`, `BackupSchema`, `BackupFileNameBuilder`; `AuditAction` values used |
| [`first-run-flow-contract.md`](first-run-flow-contract.md) | Routes (`/first-run-restore`, `/restart-required`), the `App.razor.cs` routing rule, `FirstRunLanguageScreen` navigation change, component/DOM identifiers bUnit tests bind to |
| [`backup-envelope-schema.md`](backup-envelope-schema.md) | `.sfbak` protobuf wire contract — new `[ProtoMember]` numbers, schema version `1.2.0`, older-file compatibility rules |

No REST/CLI surface — this is a desktop app. "Contract" here means C# interface signatures, Blazor route strings, protobuf field numbers, and the `.sfbak` file format.
