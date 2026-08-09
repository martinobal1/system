using IsSuas.Domain.Common;
using IsSuas.Domain.Enums;
using IsSuas.Domain.Identity;
using IsSuas.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace IsSuas.Infrastructure.Seed;

public static class DatabaseSeeder
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IsSuasDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("DatabaseSeeder");

        if (db.Database.IsRelational())
            await db.Database.MigrateAsync();
        else
            await db.Database.EnsureCreatedAsync();

        if (await db.Roles.AnyAsync())
        {
            await EnsureAccessCatalogAsync(db);
            await EnsureRetentionRulesAsync(db);
            await EnsureNotificationCatalogAsync(db);
            await db.SaveChangesAsync();
            logger.LogInformation("Database already seeded.");
            return;
        }

        var roles = new[]
        {
            new Role { Code = SystemRole.CentralGestor, NameSk = "Centrálny gestor", DescriptionSk = "Centrálne registre a metodika", GrantsBusinessDataAccess = true },
            new Role { Code = SystemRole.UnitAdministrator, NameSk = "Správca útvaru", DescriptionSk = "Údaje vlastného útvaru", GrantsBusinessDataAccess = true },
            new Role { Code = SystemRole.Approver, NameSk = "Schvaľovateľ", DescriptionSk = "Schvaľovanie operácií a registrácií", GrantsBusinessDataAccess = true },
            new Role { Code = SystemRole.RemotePilot, NameSk = "Pilot na diaľku", DescriptionSk = "Vlastný profil a operácie", GrantsBusinessDataAccess = true },
            new Role { Code = SystemRole.TechnicalWorker, NameSk = "Technický pracovník", DescriptionSk = "Údržba a technická spôsobilosť", GrantsBusinessDataAccess = true },
            new Role { Code = SystemRole.SafetyOfficer, NameSk = "Bezpečnostný pracovník", DescriptionSk = "Incidenty a opatrenia", GrantsBusinessDataAccess = true },
            new Role { Code = SystemRole.ControllerAuditor, NameSk = "Kontrolór/audítor", DescriptionSk = "Čítanie bez editácie", GrantsBusinessDataAccess = true },
            new Role { Code = SystemRole.ReadOnlyUser, NameSk = "Používateľ na čítanie", DescriptionSk = "Prehľad povolených údajov", GrantsBusinessDataAccess = true },
            new Role { Code = SystemRole.SystemAdministrator, NameSk = "Systémový administrátor", DescriptionSk = "Technická konfigurácia bez vecného prístupu", GrantsBusinessDataAccess = false }
        };
        db.Roles.AddRange(roles);

        var root = new OrgUnit { Code = "MVSR", Name = "Ministerstvo vnútra SR", Path = "/", Level = 0 };
        db.OrgUnits.Add(root);
        await db.SaveChangesAsync();
        root.Path = $"/{root.Id}/";

        var branchPz = new OrgUnit
        {
            Code = "ZLOZKA-PZ",
            Name = "Polícia — demo zložka",
            ParentId = root.Id,
            Level = 1,
            Path = "/"
        };
        db.OrgUnits.Add(branchPz);
        await db.SaveChangesAsync();
        branchPz.Path = $"{root.Path.TrimEnd('/')}/{branchPz.Id}/";

        var unitA = new OrgUnit { Code = "UTVAR-A", Name = "Útvar A", ParentId = branchPz.Id, Level = 2, Path = "/" };
        var unitB = new OrgUnit { Code = "UTVAR-B", Name = "Útvar B", ParentId = branchPz.Id, Level = 2, Path = "/" };
        db.OrgUnits.AddRange(unitA, unitB);
        await db.SaveChangesAsync();
        unitA.Path = $"{branchPz.Path.TrimEnd('/')}/{unitA.Id}/";
        unitB.Path = $"{branchPz.Path.TrimEnd('/')}/{unitB.Id}/";
        await db.SaveChangesAsync();

        var registrationFormat = new ConfigRule
        {
            Key = "uas.registration_number_format",
            ValueJson = """{"pattern":"SUAS-{YYYY}-{SEQ:5}"}""",
            EffectiveFrom = DateTimeOffset.UtcNow.AddYears(-1),
            Version = 1,
            DescriptionSk = "Formát evidenčného označenia UAS"
        };
        db.ConfigRules.Add(registrationFormat);

        var uasTypeLookup = new LookupCode { Code = "UAS_TYPE", NameSk = "Typ UAS" };
        uasTypeLookup.Items.Add(new LookupItem { ItemCode = "MULTI_ROTOR", LabelSk = "Multirotor", SortOrder = 1 });
        uasTypeLookup.Items.Add(new LookupItem { ItemCode = "FIXED_WING", LabelSk = "Pevné krídlo", SortOrder = 2 });
        db.LookupCodes.Add(uasTypeLookup);

        Account CreateAccount(string user, string first, string last, OrgUnit home)
        {
            var person = new Person { FirstName = first, LastName = last, Email = $"{user}@mv.sk.local", HomeOrgUnitId = home.Id };
            var account = new Account
            {
                Person = person,
                UserName = user,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Heslo123!"),
                Status = AccountStatus.Active
            };
            db.Persons.Add(person);
            db.Accounts.Add(account);
            return account;
        }

        var gestor = CreateAccount("gestor", "Ján", "Gestor", root);
        var approver = CreateAccount("schvalovatel", "Eva", "Schvaľovateľ", root);
        var unitAdminA = CreateAccount("spravca.a", "Peter", "Správca", unitA);
        var unitAdminB = CreateAccount("spravca.b", "Mária", "Správkyňa", unitB);
        var safety = CreateAccount("bezpecnost", "Lucia", "Bezpečnosť", root);
        var sysadmin = CreateAccount("sysadmin", "Tech", "Admin", root);
        var deactivated = CreateAccount("neaktivny", "Deaktivovaný", "Účet", unitA);

        await db.SaveChangesAsync();

        Role RoleOf(SystemRole code) => roles.First(r => r.Code == code);
        void Assign(Account account, SystemRole role, OrgUnit org)
        {
            db.RoleAssignments.Add(new RoleAssignment
            {
                AccountId = account.Id,
                RoleId = RoleOf(role).Id,
                OrgUnitId = org.Id,
                ValidFrom = DateTimeOffset.UtcNow.AddYears(-1),
                Reason = "Seed"
            });
        }

        Assign(gestor, SystemRole.CentralGestor, root);
        Assign(approver, SystemRole.Approver, root);
        Assign(unitAdminA, SystemRole.UnitAdministrator, unitA);
        Assign(unitAdminB, SystemRole.UnitAdministrator, unitB);
        Assign(safety, SystemRole.SafetyOfficer, root);
        Assign(sysadmin, SystemRole.SystemAdministrator, root);
        Assign(deactivated, SystemRole.UnitAdministrator, unitA);

        deactivated.Status = AccountStatus.Deactivated;
        deactivated.IsActive = false;

        await db.SaveChangesAsync();

        // Demo pilots for module 6.3
        var fitPerson = new Person { FirstName = "Tomáš", LastName = "Pilotný", Email = "tomas.pilotny@mv.sk.local", HomeOrgUnitId = unitA.Id };
        var unfitPerson = new Person { FirstName = "Igor", LastName = "Nespôsobilý", Email = "igor.ne@mv.sk.local", HomeOrgUnitId = unitA.Id };
        db.Persons.AddRange(fitPerson, unfitPerson);

        var fitPilot = new IsSuas.Domain.Pilots.Pilot
        {
            Person = fitPerson,
            OrgUnitId = unitA.Id,
            LicenseNumber = "PIL-001",
            Status = IsSuas.Domain.Enums.PilotStatus.Fit,
            CurrencyValidUntil = DateTimeOffset.UtcNow.AddMonths(6)
        };
        var unfitPilot = new IsSuas.Domain.Pilots.Pilot
        {
            Person = unfitPerson,
            OrgUnitId = unitA.Id,
            LicenseNumber = "PIL-002",
            Status = IsSuas.Domain.Enums.PilotStatus.Unfit
        };
        db.Pilots.AddRange(fitPilot, unfitPilot);
        await db.SaveChangesAsync();

        db.PilotCertificates.Add(new IsSuas.Domain.Pilots.PilotCertificate
        {
            PilotId = fitPilot.Id,
            CertificateTypeCode = "REMOTE_PILOT",
            TitleSk = "Osvedčenie pilota na diaľku",
            ExternalNumber = "OSV-2026-001",
            Version = 1,
            Status = IsSuas.Domain.Enums.CertificateStatus.Issued,
            IssuedAt = DateTimeOffset.UtcNow.AddMonths(-2),
            ValidFrom = DateTimeOffset.UtcNow.AddMonths(-2),
            ValidTo = DateTimeOffset.UtcNow.AddYears(1)
        });
        db.PilotCertificates.Add(new IsSuas.Domain.Pilots.PilotCertificate
        {
            PilotId = unfitPilot.Id,
            CertificateTypeCode = "REMOTE_PILOT",
            TitleSk = "Osvedčenie (exspirované)",
            Version = 1,
            Status = IsSuas.Domain.Enums.CertificateStatus.Expired,
            IssuedAt = DateTimeOffset.UtcNow.AddYears(-2),
            ValidFrom = DateTimeOffset.UtcNow.AddYears(-2),
            ValidTo = DateTimeOffset.UtcNow.AddMonths(-1)
        });

        // Demo UAS for maintenance / defects / incidents UI
        db.UasAircraft.Add(new IsSuas.Domain.Fleet.Uas
        {
            SerialNumber = "DEMO-UAS-001",
            Manufacturer = "DJI",
            Model = "M300 RTK",
            UasTypeCode = "MULTI_ROTOR",
            RegistrationNumber = "SUAS-2026-00001",
            Status = IsSuas.Domain.Enums.UasLifecycleStatus.InService,
            OwningOrgUnitId = unitA.Id,
            RegisteredAt = DateTimeOffset.UtcNow.AddMonths(-1),
            Notes = "Seed demo UAS"
        });
        db.UasAircraft.Add(new IsSuas.Domain.Fleet.Uas
        {
            SerialNumber = "DEMO-UAS-002",
            Manufacturer = "Parrot",
            Model = "Anafi USA",
            UasTypeCode = "MULTI_ROTOR",
            RegistrationNumber = "SUAS-2026-00002",
            Status = IsSuas.Domain.Enums.UasLifecycleStatus.InService,
            OwningOrgUnitId = unitA.Id,
            RegisteredAt = DateTimeOffset.UtcNow.AddMonths(-1),
            Notes = "Seed demo UAS 2"
        });

        var checklist = new IsSuas.Domain.Documents.FormTemplate
        {
            Code = "PRE_OP_CHECKLIST",
            NameSk = "Predoperačný checklist",
            Kind = IsSuas.Domain.Enums.FormTemplateKind.Checklist,
            Version = 1,
            IsCurrent = true,
            DescriptionSk = "Seed šablóna"
        };
        checklist.Items.Add(new IsSuas.Domain.Documents.FormTemplateItem
        {
            Code = "WEATHER_OK",
            LabelSk = "Počasie vhodné na let?",
            ItemType = IsSuas.Domain.Enums.FormItemType.YesNo,
            IsRequired = true,
            IsBlocking = true,
            SortOrder = 1
        });
        checklist.Items.Add(new IsSuas.Domain.Documents.FormTemplateItem
        {
            Code = "AIRSPACE_OK",
            LabelSk = "Vzdušný priestor skontrolovaný?",
            ItemType = IsSuas.Domain.Enums.FormItemType.YesNo,
            IsRequired = true,
            IsBlocking = true,
            SortOrder = 2
        });
        checklist.Items.Add(new IsSuas.Domain.Documents.FormTemplateItem
        {
            Code = "NOTES",
            LabelSk = "Poznámky",
            ItemType = IsSuas.Domain.Enums.FormItemType.Text,
            IsRequired = false,
            IsBlocking = false,
            SortOrder = 3
        });
        db.FormTemplates.Add(checklist);

        db.IntegrationConnectors.Add(new IsSuas.Domain.Integrations.IntegrationConnector
        {
            Code = "WEATHER_STUB",
            NameSk = "Počasie (stub)",
            ConnectorType = "REST",
            EndpointUrl = "https://example.invalid/weather",
            Format = "JSON",
            ScheduleCron = "0 */6 * * *",
            Status = IsSuas.Domain.Enums.IntegrationConnectorStatus.Inactive,
            SecretRef = "secrets/weather-api-key"
        });
        db.IntegrationConnectors.Add(new IsSuas.Domain.Integrations.IntegrationConnector
        {
            Code = "LPS_SR",
            NameSk = "LPS SR mapová kontrola",
            ConnectorType = "REST",
            EndpointUrl = "offline://lps-sr",
            Format = "GeoJSON",
            Status = IsSuas.Domain.Enums.IntegrationConnectorStatus.Active,
            SecretRef = null
        });
        db.IntegrationConnectors.Add(new IsSuas.Domain.Integrations.IntegrationConnector
        {
            Code = "MINIO_STORAGE",
            NameSk = "MinIO object storage",
            ConnectorType = "S3",
            EndpointUrl = "http://localhost:9000",
            Format = "BINARY",
            Status = IsSuas.Domain.Enums.IntegrationConnectorStatus.Inactive,
            SecretRef = "secrets/minio"
        });
        db.IntegrationConnectors.Add(new IsSuas.Domain.Integrations.IntegrationConnector
        {
            Code = "EMAIL_SMTP",
            NameSk = "E-mail SMTP",
            ConnectorType = "SMTP",
            EndpointUrl = "smtp://localhost:25",
            Format = "MIME",
            Status = IsSuas.Domain.Enums.IntegrationConnectorStatus.Active,
            SecretRef = null
        });

        await EnsureRetentionRulesAsync(db);
        await EnsureNotificationCatalogAsync(db);
        await db.SaveChangesAsync();
        await EnsureAccessCatalogAsync(db);
        logger.LogInformation("Seed completed. Demo password: Heslo123!");
    }

    private static async Task EnsureRetentionRulesAsync(IsSuasDbContext db)
    {
        if (await db.RetentionRules.AnyAsync(r => r.Code == "DEFAULT_5Y"))
            return;
        db.RetentionRules.Add(new Domain.Documents.RetentionRule
        {
            Code = "DEFAULT_5Y",
            NameSk = "Štandardná retencia 5 rokov",
            RetentionDays = 365 * 5,
            ArchiveMode = "ArchiveThenReview",
            DescriptionSk = "Predvolené retenčné pravidlo pre prevádzkové dokumenty"
        });
    }

    private static async Task EnsureNotificationCatalogAsync(IsSuasDbContext db)
    {
        if (!await db.MessageTemplates.AnyAsync(t => t.Code == "SAFETY_CRITICAL"))
        {
            db.MessageTemplates.Add(new Domain.Notifications.MessageTemplate
            {
                Code = "SAFETY_CRITICAL",
                NameSk = "Kritická bezpečnostná udalosť",
                SubjectTemplate = "Kritická udalosť: {title}",
                BodyTemplate = "{message}",
                Channel = NotificationChannel.InApp,
                Language = "sk"
            });
        }

        if (!await db.MessageTemplates.AnyAsync(t => t.Code == "DOCUMENT_EXPIRED"))
        {
            db.MessageTemplates.Add(new Domain.Notifications.MessageTemplate
            {
                Code = "DOCUMENT_EXPIRED",
                NameSk = "Exspirácia dokumentu",
                SubjectTemplate = "Exspirácia: {title}",
                BodyTemplate = "{message}",
                Channel = NotificationChannel.InApp,
                Language = "sk"
            });
        }

        if (!await db.EscalationRules.AnyAsync(r => r.Code == "UNREAD_24H"))
        {
            db.EscalationRules.Add(new Domain.Notifications.EscalationRule
            {
                Code = "UNREAD_24H",
                NameSk = "Neprečítaná notifikácia po 24 h",
                Trigger = EscalationTrigger.UnreadAfterHours,
                ThresholdHours = 24,
                TargetRoleCode = nameof(SystemRole.CentralGestor),
                SourceTypeCode = null,
                EscalationMessageTemplate = "Eskalácia neprečítanej notifikácie: {title}"
            });
        }

        if (!await db.EscalationRules.AnyAsync(r => r.Code == "TASK_OVERDUE_48H"))
        {
            db.EscalationRules.Add(new Domain.Notifications.EscalationRule
            {
                Code = "TASK_OVERDUE_48H",
                NameSk = "Oneskorená úloha po 48 h",
                Trigger = EscalationTrigger.TaskOverdueHours,
                ThresholdHours = 48,
                TargetRoleCode = nameof(SystemRole.UnitAdministrator),
                SourceTypeCode = null,
                EscalationMessageTemplate = "Úloha je po termíne: {title}"
            });
        }
    }

    /// <summary>ENT-007/008 — katalóg oprávnení a väzby na roly (idempotentné).</summary>
    private static async Task EnsureAccessCatalogAsync(IsSuasDbContext db)
    {
        var definitions = new (string Code, string NameSk, string Module)[]
        {
            ("orgunits.read", "Čítanie útvarov", "MOD-02"),
            ("orgunits.write", "Zápis útvarov", "MOD-02"),
            ("accounts.read", "Čítanie účtov", "MOD-16"),
            ("accounts.write", "Zápis účtov", "MOD-16"),
            ("roles.assign", "Pridelenie rolí", "MOD-16"),
            ("lookups.read", "Čítanie číselníkov", "MOD-18"),
            ("lookups.write", "Zápis číselníkov", "MOD-18"),
            ("audit.read", "Čítanie auditu", "MOD-16"),
            ("audit.export", "Export auditu", "MOD-16"),
            ("uas.read", "Čítanie UAS", "MOD-03"),
            ("uas.write", "Zápis UAS", "MOD-03"),
            ("uas.approve", "Schválenie UAS", "MOD-03"),
            ("components.read", "Čítanie komponentov", "MOD-04"),
            ("components.write", "Zápis komponentov", "MOD-04"),
            ("batteries.read", "Čítanie batérií", "MOD-05"),
            ("batteries.write", "Zápis batérií", "MOD-05"),
            ("pilots.read", "Čítanie pilotov", "MOD-06"),
            ("pilots.write", "Zápis pilotov", "MOD-06"),
            ("operations.read", "Čítanie operácií", "MOD-08"),
            ("operations.write", "Zápis operácií", "MOD-08"),
            ("operations.approve", "Schválenie operácií", "MOD-08"),
            ("flights.read", "Čítanie letov", "MOD-09"),
            ("flights.write", "Zápis letov", "MOD-09"),
            ("maintenance.read", "Čítanie údržby", "MOD-10"),
            ("maintenance.write", "Zápis údržby", "MOD-10"),
            ("safety.read", "Čítanie incidentov", "MOD-12"),
            ("safety.write", "Zápis incidentov", "MOD-12"),
            ("safety.sensitive.read", "Citlivé incidenty", "MOD-12"),
            ("safety.close", "Uzavretie incidentu", "MOD-12"),
            ("documents.read", "Čítanie dokumentov", "MOD-13"),
            ("documents.write", "Zápis dokumentov", "MOD-13"),
            ("documents.sensitive.read", "Citlivé dokumenty", "MOD-13"),
            ("reports.read", "Čítanie reportov", "MOD-14"),
            ("reports.write", "Zápis reportov", "MOD-14"),
            ("reports.official", "Oficiálne reporty", "MOD-14"),
            ("integrations.read", "Čítanie integrácií", "MOD-18"),
            ("integrations.write", "Zápis integrácií", "MOD-18"),
            ("system.info.read", "Systémové info", "MOD-18"),
            ("controls.read", "Čítanie kontrol", "MOD-15"),
            ("controls.write", "Zápis kontrol", "MOD-15"),
            ("controls.close", "Uzavretie kontrol", "MOD-15"),
            ("notifications.read", "Čítanie notifikácií", "MOD-11"),
            ("notifications.write", "Zápis notifikácií", "MOD-11")
        };

        var existing = await db.Permissions.ToDictionaryAsync(p => p.Code, StringComparer.Ordinal);
        foreach (var (code, name, module) in definitions)
        {
            if (existing.ContainsKey(code)) continue;
            var perm = new Permission { Code = code, NameSk = name, ModuleCode = module };
            db.Permissions.Add(perm);
            existing[code] = perm;
        }
        await db.SaveChangesAsync();

        // Refresh ids
        existing = await db.Permissions.ToDictionaryAsync(p => p.Code, StringComparer.Ordinal);
        var roles = await db.Roles.ToListAsync();
        var existingLinks = await db.RolePermissions.Select(rp => new { rp.RoleId, rp.PermissionId }).ToListAsync();
        var linkSet = existingLinks.Select(x => (x.RoleId, x.PermissionId)).ToHashSet();

        // Mirror AuthorizationPolicyService map for seed (DB catalog; runtime still uses policy map + DB).
        var rolePerms = new Dictionary<SystemRole, string[]>
        {
            [SystemRole.CentralGestor] = existing.Keys.ToArray(),
            [SystemRole.ControllerAuditor] =
            [
                "orgunits.read", "accounts.read", "lookups.read", "audit.read", "audit.export",
                "uas.read", "components.read", "batteries.read", "pilots.read", "operations.read",
                "flights.read", "maintenance.read", "safety.read", "safety.sensitive.read",
                "documents.read", "documents.sensitive.read", "reports.read", "integrations.read",
                "system.info.read", "controls.read", "controls.write", "controls.close",
                "notifications.read"
            ],
            [SystemRole.UnitAdministrator] =
            [
                "orgunits.read", "orgunits.write", "accounts.read", "accounts.write", "roles.assign",
                "lookups.read", "uas.read", "uas.write", "components.read", "components.write",
                "batteries.read", "batteries.write", "pilots.read", "pilots.write", "operations.read",
                "operations.write", "flights.read", "flights.write", "maintenance.read", "maintenance.write",
                "safety.read", "safety.write", "documents.read", "documents.write", "reports.read",
                "reports.write", "system.info.read", "controls.read",
                "notifications.read", "notifications.write"
            ],
            [SystemRole.Approver] =
            [
                "lookups.read", "uas.read", "uas.approve", "pilots.read", "operations.read",
                "operations.approve", "flights.read", "safety.read", "documents.read", "reports.read",
                "reports.official", "notifications.read"
            ],
            [SystemRole.SystemAdministrator] =
            [
                "orgunits.read", "accounts.read", "lookups.read", "lookups.write", "audit.read",
                "audit.export", "integrations.read", "integrations.write", "system.info.read"
            ]
        };

        foreach (var role in roles)
        {
            if (!rolePerms.TryGetValue(role.Code, out var codes))
                continue;
            foreach (var code in codes)
            {
                if (!existing.TryGetValue(code, out var perm)) continue;
                if (linkSet.Contains((role.Id, perm.Id))) continue;
                db.RolePermissions.Add(new RolePermission { RoleId = role.Id, PermissionId = perm.Id });
                linkSet.Add((role.Id, perm.Id));
            }
        }

        await db.SaveChangesAsync();
    }
}
