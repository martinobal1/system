using IsSuas.Domain.Common;
using IsSuas.Domain.Enums;
using IsSuas.Domain.Identity;
using IsSuas.Domain.Reporting;
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

        if (db.Database.ProviderName == "Microsoft.EntityFrameworkCore.Sqlite")
        {
            await db.Database.EnsureCreatedAsync();
            await EnsureSqlitePilotCertificateColumnsAsync(db);
            logger.LogInformation("SQLite databáza: {Path}", db.Database.GetDbConnection().DataSource);
        }
        else if (db.Database.IsRelational())
            await db.Database.MigrateAsync();
        else
            await db.Database.EnsureCreatedAsync();

        if (await db.Roles.AnyAsync())
        {
            try
            {
                await RefreshSeededDatabaseAsync(db, logger);
                logger.LogInformation("Database already seeded.");
            }
            catch (DbUpdateConcurrencyException ex)
            {
                // Súbežný zápis do rovnakej DB (napr. druhá bežiaca instancia API) nesmie zhodiť štart API.
                logger.LogWarning(ex, "Priebežná aktualizácia seed dát bola preskočená pre súbežnú zmenu v databáze.");
            }
            return;
        }

        await SeedFreshDatabaseAsync(db, logger);
    }

    private static async Task RefreshSeededDatabaseAsync(IsSuasDbContext db, ILogger logger)
    {
        await EnsureDemoLoginPasswordsAsync(db, logger);
        await EnsureAccessLevelsAsync(db);
        await EnsureAccessCatalogAsync(db);
        await EnsureRetentionRulesAsync(db);
        await EnsureNotificationCatalogAsync(db);
        await EnsureAtlasOrgStructureAsync(db);
        var existingBranch = await db.OrgUnits.FirstOrDefaultAsync(o => o.Code == "ZLOZKA-PZ");
        var existingA = await db.OrgUnits.FirstOrDefaultAsync(o => o.Code == "UTVAR-A");
        var existingB = await db.OrgUnits.FirstOrDefaultAsync(o => o.Code == "UTVAR-B");
        if (existingBranch is not null && existingA is not null && existingB is not null)
        {
            var existingKrs = await EnsureKrOrgUnitsAsync(db, existingBranch);
            var existingPool = existingKrs.Count > 0
                ? existingKrs.Select(k => k.Id).ToList()
                : new List<Guid> { existingA.Id, existingB.Id };
            await EnsureDemoFleetVolumeAsync(db, existingPool, existingA.Id, existingB.Id);
            await EnsureDemoPilotsAndFlightsAsync(db, existingKrs.Count > 0 ? existingKrs : new List<OrgUnit> { existingA, existingB });
            await EnsureA1A3OnlyDemoPilotsAsync(db, existingKrs.Count > 0 ? existingKrs : new List<OrgUnit> { existingA, existingB });
            try
            {
                await EnsurePilotOpenCategoryCertificatesAsync(db);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Doplnenie osvedčení PnD pri seede preskočené.");
            }
            await EnsurePilotUasAuthorizationsAsync(db);
            await EnsureDemoMonthlyReportsAsync(db);
            var existingGestor = await db.Accounts.FirstOrDefaultAsync(a => a.UserName == "gestor");
            if (existingGestor is not null)
            {
                await EnsureDemoDefectsAsync(db, existingGestor.Id);
                await EnsureDemoCalendarOperationsAsync(db, existingGestor.Id,
                    existingKrs.Count > 0 ? existingKrs : new List<OrgUnit> { existingA, existingB });
                await EnsureDemoCriticalAlertsAsync(db, existingGestor.Id);
            }
        }
        await EnsureNamedLoginUsersAsync(db, logger);
        await EnsureLegacySeedOperatorsDemotedAsync(db);
        await EnsureFlightGpsLocationsAsync(db, logger);
        await EnsureFleetUsageDemoSplitAsync(db);
        await db.SaveChangesAsync();
    }

    private static async Task SeedFreshDatabaseAsync(IsSuasDbContext db, ILogger logger)
    {
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

        var root = new OrgUnit { Code = "MVSR", Name = "Ministerstvo vnútra SR", Kind = OrgUnitKind.Root, Path = "/", Level = 0 };
        db.OrgUnits.Add(root);
        await db.SaveChangesAsync();
        root.Path = $"/{root.Id}/";

        var branchPz = new OrgUnit
        {
            Code = "ZLOZKA-PZ",
            Name = "Policajný zbor",
            Kind = OrgUnitKind.BranchPz,
            ParentId = root.Id,
            Level = 1,
            Path = "/"
        };
        var branchHazz = new OrgUnit
        {
            Code = "ZLOZKA-HAZZ",
            Name = "Hasičský a záchranný zbor",
            Kind = OrgUnitKind.BranchHazz,
            ParentId = root.Id,
            Level = 1,
            Path = "/"
        };
        var branchCo = new OrgUnit
        {
            Code = "ZLOZKA-CO",
            Name = "Civilná ochrana",
            Kind = OrgUnitKind.BranchCo,
            ParentId = root.Id,
            Level = 1,
            Path = "/"
        };
        db.OrgUnits.AddRange(branchPz, branchHazz, branchCo);
        await db.SaveChangesAsync();
        branchPz.Path = $"{root.Path.TrimEnd('/')}/{branchPz.Id}/";
        branchHazz.Path = $"{root.Path.TrimEnd('/')}/{branchHazz.Id}/";
        branchCo.Path = $"{root.Path.TrimEnd('/')}/{branchCo.Id}/";

        var unitA = new OrgUnit { Code = "UTVAR-A", Name = "Útvar A", Kind = OrgUnitKind.Unit, ParentId = branchPz.Id, Level = 2, Path = "/" };
        var unitB = new OrgUnit { Code = "UTVAR-B", Name = "Útvar B", Kind = OrgUnitKind.Unit, ParentId = branchPz.Id, Level = 2, Path = "/" };
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

        Account CreateAccount(string user, string first, string last, OrgUnit home, AccessLevel level = AccessLevel.Level2, string password = "Heslo123!")
        {
            var person = new Person { FirstName = first, LastName = last, Email = $"{user}@mv.sk.local", HomeOrgUnitId = home.Id };
            var account = new Account
            {
                Person = person,
                UserName = user,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
                Status = AccountStatus.Active,
                AccessLevel = level,
                LicenseStatus = level == AccessLevel.Level3 ? LicenseStatus.Confirmed : LicenseStatus.NotApplicable
            };
            db.Persons.Add(person);
            db.Accounts.Add(account);
            return account;
        }

        var gestor = CreateAccount("gestor", "Ján", "Gestor", root, AccessLevel.Level1, "huginko123");
        var approver = CreateAccount("schvalovatel", "Eva", "Schvaľovateľ", root, AccessLevel.Level1);
        var unitAdminA = CreateAccount("spravca.a", "Peter", "Správca", unitA, AccessLevel.Level1);
        var unitAdminB = CreateAccount("spravca.b", "Mária", "Správkyňa", unitB, AccessLevel.Level1);
        var safety = CreateAccount("bezpecnost", "Lucia", "Bezpečnosť", root, AccessLevel.Level1);
        var sysadmin = CreateAccount("sysadmin", "Tech", "Admin", root, AccessLevel.Level1);
        var deactivated = CreateAccount("neaktivny", "Deaktivovaný", "Účet", unitA, AccessLevel.Level3);

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
        Assign(unitAdminA, SystemRole.TechnicalWorker, unitA);
        Assign(unitAdminB, SystemRole.TechnicalWorker, unitB);
        Assign(safety, SystemRole.SafetyOfficer, root);
        Assign(sysadmin, SystemRole.SystemAdministrator, root);
        Assign(deactivated, SystemRole.RemotePilot, unitA);

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
            CertificateTypeCode = "A1_A3",
            TitleSk = "Preukaz pilota na diaľku — kategória A1/A3",
            ExternalNumber = "OSV-A1A3-2026-001",
            Version = 1,
            Status = IsSuas.Domain.Enums.CertificateStatus.Issued,
            IssuedAt = DateTimeOffset.UtcNow.AddMonths(-2),
            ValidFrom = DateTimeOffset.UtcNow.AddMonths(-2),
            ValidTo = DateTimeOffset.UtcNow.AddYears(1)
        });
        db.PilotCertificates.Add(new IsSuas.Domain.Pilots.PilotCertificate
        {
            PilotId = fitPilot.Id,
            CertificateTypeCode = "A2",
            TitleSk = "Preukaz pilota na diaľku — kategória A2",
            ExternalNumber = "OSV-A2-2026-001",
            Version = 1,
            Status = IsSuas.Domain.Enums.CertificateStatus.Issued,
            IssuedAt = DateTimeOffset.UtcNow.AddMonths(-1),
            ValidFrom = DateTimeOffset.UtcNow.AddMonths(-1),
            ValidTo = DateTimeOffset.UtcNow.AddYears(1)
        });
        db.PilotCertificates.Add(new IsSuas.Domain.Pilots.PilotCertificate
        {
            PilotId = unfitPilot.Id,
            CertificateTypeCode = "A1_A3",
            TitleSk = "Preukaz A1/A3 (exspirovaný)",
            Version = 1,
            Status = IsSuas.Domain.Enums.CertificateStatus.Expired,
            IssuedAt = DateTimeOffset.UtcNow.AddYears(-2),
            ValidFrom = DateTimeOffset.UtcNow.AddYears(-2),
            ValidTo = DateTimeOffset.UtcNow.AddMonths(-1)
        });

        // Demo UAS / komponenty / batérie / KR / piloti / lety
        var krs = await EnsureKrOrgUnitsAsync(db, branchPz);
        var orgPool = krs.Select(k => k.Id).ToList();
        await EnsureDemoFleetVolumeAsync(db, orgPool, unitA.Id, unitB.Id);
        await EnsureDemoPilotsAndFlightsAsync(db, krs);
        await EnsureA1A3OnlyDemoPilotsAsync(db, krs);
        await EnsurePilotOpenCategoryCertificatesAsync(db);
        await EnsurePilotUasAuthorizationsAsync(db);
        await EnsureDemoMonthlyReportsAsync(db);
        await EnsureDemoDefectsAsync(db, gestor.Id);
        await EnsureDemoCalendarOperationsAsync(db, gestor.Id, krs);
        await EnsureDemoCriticalAlertsAsync(db, gestor.Id);

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
        await EnsureNamedLoginUsersAsync(db, logger);
        await EnsureLegacySeedOperatorsDemotedAsync(db);
        await EnsureFlightGpsLocationsAsync(db, logger);
        await EnsureFleetUsageDemoSplitAsync(db);
        await EnsureDemoLoginPasswordsAsync(db, logger);
        logger.LogInformation("Seed completed.");
    }

    private static async Task EnsureDemoLoginPasswordsAsync(IsSuasDbContext db, ILogger logger)
    {
        var passwords = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["gestor"] = "huginko123",
            ["schvalovatel"] = "Heslo123!",
            ["spravca.a"] = "Heslo123!",
            ["spravca.b"] = "Heslo123!",
            ["bezpecnost"] = "Heslo123!",
            ["sysadmin"] = "Heslo123!",
            ["martin"] = "3XXAgVWi#MKxQ2bY",
            ["janko.novak"] = "!tt!DKhENEcE7esB"
        };

        var names = passwords.Keys.ToList();
        var accounts = await db.Accounts.Where(a => names.Contains(a.UserName)).ToListAsync();
        var updated = 0;
        foreach (var account in accounts)
        {
            if (!passwords.TryGetValue(account.UserName, out var password))
                continue;

            var changed = false;
            if (!BCrypt.Net.BCrypt.Verify(password, account.PasswordHash))
            {
                account.PasswordHash = BCrypt.Net.BCrypt.HashPassword(password);
                changed = true;
            }
            if (!account.IsActive)
            {
                account.IsActive = true;
                changed = true;
            }
            if (account.Status is AccountStatus.Deactivated or AccountStatus.TemporarilyBlocked or AccountStatus.Prepared)
            {
                account.Status = AccountStatus.Active;
                changed = true;
            }
            if (changed)
                updated++;
        }

        if (updated > 0)
        {
            await db.SaveChangesAsync();
            logger.LogInformation("Obnovené demo heslá pre {Count} účtov.", updated);
        }
    }

    /// <summary>
    /// Seed demo účty spravca.* už nie sú L2 prevádzkovatelia — ostáva len janko.novak (a ďalší vytvorení L2).
    /// </summary>
    private static async Task EnsureLegacySeedOperatorsDemotedAsync(IsSuasDbContext db)
    {
        var techRole = await db.Roles.FirstOrDefaultAsync(r => r.Code == SystemRole.TechnicalWorker && r.IsActive);
        var pilotRole = await db.Roles.FirstOrDefaultAsync(r => r.Code == SystemRole.RemotePilot && r.IsActive);
        if (techRole is null) return;

        var legacy = await db.Accounts
            .Include(a => a.RoleAssignments)
            .Where(a => a.UserName == "spravca.a" || a.UserName == "spravca.b" || a.UserName == "neaktivny")
            .ToListAsync();

        foreach (var account in legacy)
        {
            foreach (var ra in account.RoleAssignments.Where(r => r.IsActive))
            {
                var role = await db.Roles.FirstOrDefaultAsync(r => r.Id == ra.RoleId);
                if (role?.Code != SystemRole.UnitAdministrator) continue;
                ra.IsActive = false;
                ra.ValidTo = DateTimeOffset.UtcNow;
                db.RoleAssignments.Add(new RoleAssignment
                {
                    AccountId = account.Id,
                    RoleId = account.UserName == "neaktivny" && pilotRole is not null ? pilotRole.Id : techRole.Id,
                    OrgUnitId = ra.OrgUnitId,
                    ValidFrom = DateTimeOffset.UtcNow.AddYears(-1),
                    Reason = "Demote seed L2 — prevádzkovateľ je janko.novak"
                });
            }
        }

        await db.SaveChangesAsync();
        await EnsureAccessLevelsAsync(db);
    }

    /// <summary>
    /// Doplní chýbajúce GPS súradnice v denníku letov a normalizuje Location na „lat, lon“.
    /// </summary>
    private static async Task EnsureFlightGpsLocationsAsync(IsSuasDbContext db, ILogger logger)
    {
        var spots = new (double Lat, double Lon)[]
        {
            (48.17380, 16.97820), (48.17000, 17.20000), (48.37740, 17.58830), (48.59000, 17.83000),
            (48.34420, 18.09630), (47.76300, 18.12800), (48.89550, 18.05230), (48.77500, 18.62500),
            (49.22300, 18.73900), (49.06500, 18.92200), (48.73200, 19.14000), (48.63900, 19.13900),
            (48.99800, 21.24000), (49.05500, 20.29700), (48.73600, 21.21200), (48.75500, 21.92000),
            (47.98600, 18.16300), (49.43800, 18.78900), (48.21500, 18.60700), (48.94500, 20.56800),
            (48.12550, 17.11050), (48.14860, 17.10770), (49.18700, 18.86200), (48.71640, 21.26110)
        };

        var flights = await db.Flights
            .Where(f => f.IsActive)
            .OrderBy(f => f.FlightNumber)
            .ThenBy(f => f.Id)
            .ToListAsync();

        var fixedMissing = 0;
        var normalized = 0;
        for (var i = 0; i < flights.Count; i++)
        {
            var f = flights[i];
            var hasLat = f.LocationLat is >= -90 and <= 90;
            var hasLon = f.LocationLon is >= -180 and <= 180;
            if (!hasLat || !hasLon)
            {
                // Skús vytiahnuť zo starého textu „… 48.12°N, 17.11°E …“
                if (!TryParseGpsFromLocation(f.Location, out var parsedLat, out var parsedLon))
                {
                    var s = spots[i % spots.Length];
                    // jemné rozptýlenie podľa indexu, aby neboli všetky rovnaké
                    parsedLat = s.Lat + ((i % 7) - 3) * 0.0021;
                    parsedLon = s.Lon + ((i % 5) - 2) * 0.0027;
                }
                f.LocationLat = Math.Round(parsedLat, 5);
                f.LocationLon = Math.Round(parsedLon, 5);
                if (f.LocationRadiusMeters is null or < 50)
                    f.LocationRadiusMeters = 1000;
                fixedMissing++;
            }

            var gpsText = $"{f.LocationLat:0.00000}, {f.LocationLon:0.00000}";
            if (!string.Equals(f.Location, gpsText, StringComparison.Ordinal))
            {
                f.Location = gpsText;
                normalized++;
            }
        }

        if (fixedMissing > 0 || normalized > 0)
        {
            await db.SaveChangesAsync();
            logger.LogInformation(
                "Flight GPS backfill: {Missing} missing coords filled, {Normalized} location texts normalized.",
                fixedMissing, normalized);
        }
    }

    private static bool TryParseGpsFromLocation(string? location, out double lat, out double lon)
    {
        lat = 0;
        lon = 0;
        if (string.IsNullOrWhiteSpace(location)) return false;

        // „48.12550, 17.11050“
        var simple = System.Text.RegularExpressions.Regex.Match(
            location,
            @"^\s*(-?\d+(?:\.\d+)?)\s*,\s*(-?\d+(?:\.\d+)?)\s*$");
        if (simple.Success &&
            double.TryParse(simple.Groups[1].Value, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out lat) &&
            double.TryParse(simple.Groups[2].Value, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out lon))
            return lat is >= -90 and <= 90 && lon is >= -180 and <= 180;

        // „… 48.1255°N, 17.1105°E …“
        var deg = System.Text.RegularExpressions.Regex.Match(
            location,
            @"(-?\d+(?:\.\d+)?)\s*°?\s*([NnSs])\s*[,;\s]+\s*(-?\d+(?:\.\d+)?)\s*°?\s*([EeWw])",
            System.Text.RegularExpressions.RegexOptions.CultureInvariant);
        if (deg.Success &&
            double.TryParse(deg.Groups[1].Value, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out lat) &&
            double.TryParse(deg.Groups[3].Value, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out lon))
        {
            if (deg.Groups[2].Value is "S" or "s") lat = -lat;
            if (deg.Groups[4].Value is "W" or "w") lon = -lon;
            return lat is >= -90 and <= 90 && lon is >= -180 and <= 180;
        }

        // „… 48.1255, 17.1105 …“ vnútri textu
        var embedded = System.Text.RegularExpressions.Regex.Match(
            location,
            @"(-?\d{1,2}\.\d{2,})\s*,\s*(-?\d{1,3}\.\d{2,})");
        if (embedded.Success &&
            double.TryParse(embedded.Groups[1].Value, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out lat) &&
            double.TryParse(embedded.Groups[2].Value, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out lon))
            return lat is >= -90 and <= 90 && lon is >= -180 and <= 180;

        return false;
    }

    /// <summary>
    /// Oficiálny register UAS podľa reálneho inventára (počty modelov).
    /// </summary>
    private static async Task EnsureOfficialUasInventoryAsync(
        IsSuasDbContext db,
        IReadOnlyList<Guid> orgPool,
        DateTimeOffset now,
        Random rng)
    {
        var fleet = new (string Maker, string Model, string Type, int Count)[]
        {
            ("DJI", "Mavic 2 Enterprise Dual", "MULTI_ROTOR", 13),
            ("DJI", "Mavic 2 Zoom", "MULTI_ROTOR", 54),
            ("DJI", "Matrice 300 RTK", "MULTI_ROTOR", 11),
            ("DJI", "Matrice 350 RTK", "MULTI_ROTOR", 29),
            ("DJI", "Matrice 30T", "MULTI_ROTOR", 9),
            ("DJI", "Mavic 3E", "MULTI_ROTOR", 26),
            ("DJI", "Mavic 3T", "MULTI_ROTOR", 39),
            ("DJI", "Mavic 2 Pro", "MULTI_ROTOR", 1),
            ("Loki", "MKII", "MULTI_ROTOR", 2),
            ("DJI", "Matrice 600 Pro", "MULTI_ROTOR", 1),
            ("Primoco", "One 150", "FIXED_WING", 2),
            ("DJI", "Avata 2", "MULTI_ROTOR", 20),
            ("DJI", "Mini 4 Pro", "MULTI_ROTOR", 14),
            ("DJI", "Mavic 4 Pro", "MULTI_ROTOR", 5),
            ("DJI", "Matrice 4T", "MULTI_ROTOR", 2),
            ("DJI", "Matrice 4TD", "MULTI_ROTOR", 3),
            ("DJI", "Mini 5 Pro", "MULTI_ROTOR", 9),
            ("DJI", "FlyCart 30", "MULTI_ROTOR", 4),
            ("DJI", "Mavic 3 Pro", "MULTI_ROTOR", 1),
            ("DJI", "Matrice 210 RTK", "MULTI_ROTOR", 2)
        };

        var expected = fleet.Sum(x => x.Count);
        var officialCount = await db.UasAircraft.CountAsync(u =>
            u.IsActive && u.Notes != null && u.Notes.StartsWith("SEED-REG-"));
        if (officialCount >= expected)
            return;

        // Vyradiť staré demo UAS zo zoznamu (neoficiálny seed)
        var demos = await db.UasAircraft
            .Where(u => u.IsActive && (
                u.SerialNumber.StartsWith("DEMO-UAS-") ||
                (u.Notes != null && u.Notes.StartsWith("Seed demo"))))
            .ToListAsync();
        foreach (var d in demos)
            d.IsActive = false;
        if (demos.Count > 0)
            await db.SaveChangesAsync();

        officialCount = await db.UasAircraft.CountAsync(u =>
            u.IsActive && u.Notes != null && u.Notes.StartsWith("SEED-REG-"));
        if (officialCount >= expected)
            return;

        static string ModelSlug(string model) =>
            model.Replace(" ", "", StringComparison.Ordinal)
                .Replace("-", "", StringComparison.Ordinal)
                .ToUpperInvariant();

        var seq = officialCount;
        foreach (var spec in fleet)
        {
            var existingOfModel = await db.UasAircraft.CountAsync(u =>
                u.IsActive &&
                u.Notes != null && u.Notes.StartsWith("SEED-REG-") &&
                u.Manufacturer == spec.Maker &&
                u.Model == spec.Model);
            var need = spec.Count - existingOfModel;
            if (need <= 0) continue;

            var slug = ModelSlug(spec.Model);
            for (var i = 1; i <= need; i++)
            {
                seq++;
                var unitIdx = ((seq - 1) % orgPool.Count);
                // ~3 % vyradených pre ukážku stĺpca „Dátum výmazu“
                var decommissioned = seq % 33 == 0;
                db.UasAircraft.Add(new Domain.Fleet.Uas
                {
                    SerialNumber = $"SN-{slug}-{existingOfModel + i:000}-{seq:00000}",
                    Manufacturer = spec.Maker,
                    Model = spec.Model,
                    UasTypeCode = spec.Type,
                    RegistrationNumber = $"OM-UAS-2026-{seq:00000}",
                    CertificateNumber = $"OSV-2026-{seq:00000}",
                    LogbookNumber = $"DN-2026-{seq:00000}",
                    InventoryAssetNumber = $"IM-2026-{seq:00000}",
                    Status = decommissioned ? UasLifecycleStatus.Decommissioned : UasLifecycleStatus.InService,
                    OwningOrgUnitId = orgPool[unitIdx],
                    RegisteredAt = now.AddDays(-rng.Next(30, 900)),
                    DeregisteredAt = decommissioned ? now.AddDays(-rng.Next(1, 120)) : null,
                    Notes = $"SEED-REG-{seq:00000}",
                    TotalFlightCount = decommissioned ? rng.Next(10, 80) : rng.Next(0, 200),
                    TotalFlightMinutes = decommissioned ? rng.Next(100, 2000) : rng.Next(0, 5000)
                });
            }
        }

        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Doplní demo flotilu na aspoň 50 UAS / 50 komponentov / 50 batérií (idempotentné).
    /// </summary>
    private static async Task EnsureDemoFleetVolumeAsync(
        IsSuasDbContext db,
        IReadOnlyList<Guid> orgUnitIds,
        Guid fallbackA,
        Guid fallbackB)
    {
        const int target = 50;
        var now = DateTimeOffset.UtcNow;
        var rng = new Random(20260809);
        var pool = orgUnitIds.Count > 0 ? orgUnitIds : new[] { fallbackA, fallbackB };

        Guid OrgFor(int i) => pool[(i - 1) % pool.Count];

        await EnsureOfficialUasInventoryAsync(db, pool, now, rng);

        // Komponenty / batérie: starý demo seed vypnutý — Technická dokumentácia ide cez UAS modely.
        await ClearLegacyComponentCatalogAsync(db);

        var assetStatuses = new[]
        {
            AssetUsabilityStatus.Available, AssetUsabilityStatus.Available, AssetUsabilityStatus.Available,
            AssetUsabilityStatus.InUse, AssetUsabilityStatus.Suspended, AssetUsabilityStatus.Damaged
        };

        var batteryModels = new (string Maker, string Model)[]
        {
            ("DJI", "TB60"),
            ("DJI", "TB65"),
            ("DJI", "Intelligent Flight Battery"),
            ("Parrot", "Anafi USA Battery"),
            ("Autel", "Evo Max Battery"),
            ("Skydio", "X2 Battery"),
            ("Freefly", "Astro Battery"),
            ("Generic", "LiPo 6S 10000mAh")
        };

        var existingBat = await db.Batteries.CountAsync(x => x.IsActive);
        if (existingBat < target)
        {
            var next = existingBat + 1;
            for (var i = next; i <= target; i++)
            {
                var m = batteryModels[(i - 1) % batteryModels.Length];
                var health = Math.Round((decimal)(55 + rng.NextDouble() * 45), 1);
                db.Batteries.Add(new Domain.Fleet.Battery
                {
                    InternalNumber = $"BAT-{i:000}",
                    SerialNumber = $"SN-BAT-{20000 + i}",
                    Manufacturer = m.Maker,
                    Model = m.Model,
                    CompatibleUasTypeCodes = i % 5 == 0 ? "FIXED_WING" : "MULTI_ROTOR",
                    Status = assetStatuses[(i - 1) % assetStatuses.Length],
                    OwningOrgUnitId = OrgFor(i),
                    CycleCount = rng.Next(0, 320),
                    CapacityPercent = Math.Round((decimal)(40 + rng.NextDouble() * 60), 1),
                    HealthPercent = health,
                    MaxTemperatureC = Math.Round((decimal)(35 + rng.NextDouble() * 20), 1),
                    MinTemperatureC = Math.Round((decimal)(-5 + rng.NextDouble() * 15), 1)
                });
            }
        }

        await db.SaveChangesAsync();
    }

    /// <summary>Odstráni starý demo katalóg komponentov — nahrádza ho Technická dokumentácia podľa modelov UAS.</summary>
    private static async Task ClearLegacyComponentCatalogAsync(IsSuasDbContext db)
    {
        var comps = await db.Components.Where(c => c.IsActive).ToListAsync();
        if (comps.Count == 0) return;
        foreach (var c in comps)
        {
            c.IsActive = false;
            c.Status = AssetUsabilityStatus.Decommissioned;
        }
        await db.SaveChangesAsync();
    }

    private static async Task<List<OrgUnit>> EnsureKrOrgUnitsAsync(IsSuasDbContext db, OrgUnit branchPz)
    {
        var defs = new (string Code, string Name)[]
        {
            ("KR-BA", "Krajské riaditeľstvo Bratislava"),
            ("KR-TT", "Krajské riaditeľstvo Trnava"),
            ("KR-NR", "Krajské riaditeľstvo Nitra"),
            ("KR-TN", "Krajské riaditeľstvo Trenčín"),
            ("KR-ZA", "Krajské riaditeľstvo Žilina"),
            ("KR-BB", "Krajské riaditeľstvo Banská Bystrica"),
            ("KR-PO", "Krajské riaditeľstvo Prešov"),
            ("KR-KE", "Krajské riaditeľstvo Košice")
        };

        var result = new List<OrgUnit>();
        foreach (var (code, name) in defs)
        {
            var existing = await db.OrgUnits.FirstOrDefaultAsync(o => o.Code == code);
            if (existing is null)
            {
                existing = new OrgUnit
                {
                    Code = code,
                    Name = name,
                    Kind = OrgUnitKind.Unit,
                    ParentId = branchPz.Id,
                    Level = 2,
                    Path = "/"
                };
                db.OrgUnits.Add(existing);
                await db.SaveChangesAsync();
                existing.Path = $"{branchPz.Path.TrimEnd('/')}/{existing.Id}/";
                await db.SaveChangesAsync();
            }
            result.Add(existing);
        }

        return result;
    }

    /// <summary>
    /// 200 pilotov (4 na dron), každý viazaný na jeden UAS + uzavreté lety pre nalietané hodiny.
    /// </summary>
    private static async Task EnsureDemoPilotsAndFlightsAsync(IsSuasDbContext db, IReadOnlyList<OrgUnit> krUnits)
    {
        if (krUnits.Count == 0) return;

        const int pilotTarget = 200;
        var seedPilotCount = await db.Pilots.CountAsync(p => p.LicenseNumber != null && p.LicenseNumber.StartsWith("PIL-SEED-"));
        var drones = await db.UasAircraft.AsNoTracking()
            .Where(u => u.IsActive && u.Status != UasLifecycleStatus.Draft && u.Status != UasLifecycleStatus.Decommissioned)
            .OrderBy(u => u.SerialNumber)
            .ToListAsync();
        if (drones.Count == 0)
            drones = await db.UasAircraft.AsNoTracking().Where(u => u.IsActive).OrderBy(u => u.SerialNumber).ToListAsync();
        if (drones.Count == 0) return;

        var firstNames = new[]
        {
            "Adam", "Peter", "Martin", "Jozef", "Michal", "Tomáš", "Lukáš", "Marek", "Jakub", "Samuel",
            "Filip", "Matej", "Patrik", "Richard", "Ondrej", "Roman", "Erik", "Daniel", "Dominik", "Vladimír",
            "Anna", "Mária", "Eva", "Zuzana", "Katarína", "Jana", "Lucia", "Monika", "Lenka", "Petra"
        };
        var lastNames = new[]
        {
            "Horváth", "Nagy", "Szabó", "Kováč", "Varga", "Tóth", "Molnár", "Baláž", "Lukáč", "Novák",
            "Horváthová", "Kováčová", "Szabová", "Nagyová", "Tóthová", "Balážová", "Vargová", "Molnárová",
            "Urban", "Polák", "Šimko", "Hudák", "Gajdoš", "Černák", "Bartoš", "Oravec", "Krajčí", "Farkaš"
        };

        var rng = new Random(20260809);
        var now = DateTimeOffset.UtcNow;
        var pilots = new List<(Domain.Pilots.Pilot Pilot, Guid UasId, Guid OrgUnitId)>();

        if (seedPilotCount < pilotTarget)
        {
            var next = seedPilotCount + 1;
            for (var i = next; i <= pilotTarget; i++)
            {
                var drone = drones[(i - 1) % drones.Count];
                var org = krUnits[(i - 1) % krUnits.Count];
                // Prefer matching drone owning org when it is a KR.
                var droneOrg = krUnits.FirstOrDefault(k => k.Id == drone.OwningOrgUnitId) ?? org;
                var fn = firstNames[(i + rng.Next(0, 3)) % firstNames.Length];
                var ln = lastNames[(i * 3) % lastNames.Length];
                var person = new Person
                {
                    FirstName = fn,
                    LastName = ln,
                    Email = $"pilot.seed.{i:000}@mv.sk.local",
                    HomeOrgUnitId = droneOrg.Id
                };
                db.Persons.Add(person);
                var pilot = new Domain.Pilots.Pilot
                {
                    Person = person,
                    OrgUnitId = droneOrg.Id,
                    LicenseNumber = $"PIL-SEED-{i:000}",
                    Status = PilotStatus.Fit,
                    CurrencyValidUntil = now.AddMonths(6 + (i % 12)),
                    Notes = $"PrimaryUas:{drone.SerialNumber}"
                };
                db.Pilots.Add(pilot);
                pilots.Add((pilot, drone.Id, droneOrg.Id));
            }
            await db.SaveChangesAsync();
        }
        else
        {
            var existing = await db.Pilots.AsNoTracking()
                .Where(p => p.LicenseNumber != null && p.LicenseNumber.StartsWith("PIL-SEED-"))
                .OrderBy(p => p.LicenseNumber)
                .ToListAsync();
            foreach (var p in existing)
            {
                var serial = (p.Notes ?? "").Replace("PrimaryUas:", "", StringComparison.Ordinal);
                var drone = drones.FirstOrDefault(d => d.SerialNumber == serial) ?? drones[(pilots.Count) % drones.Count];
                pilots.Add((p, drone.Id, p.OrgUnitId));
            }
        }

        if (await db.Flights.AnyAsync(f => f.StandaloneReason == "SEED-VOLUME" || f.StandaloneReason == "SEED-DIARY"))
            return;

        // Denník: presne 100 uzavretých letov (pilot lietá len na svojom drone).
        var locations = new (string Gps, double Lat, double Lon)[]
        {
            ("48.17380, 16.97820", 48.17380, 16.97820),
            ("48.17000, 17.20000", 48.17000, 17.20000),
            ("48.37740, 17.58830", 48.37740, 17.58830),
            ("48.59000, 17.83000", 48.59000, 17.83000),
            ("48.34420, 18.09630", 48.34420, 18.09630),
            ("47.76300, 18.12800", 47.76300, 18.12800),
            ("48.89550, 18.05230", 48.89550, 18.05230),
            ("48.77500, 18.62500", 48.77500, 18.62500),
            ("49.22300, 18.73900", 49.22300, 18.73900),
            ("49.06500, 18.92200", 49.06500, 18.92200),
            ("48.73200, 19.14000", 48.73200, 19.14000),
            ("48.63900, 19.13900", 48.63900, 19.13900),
            ("48.99800, 21.24000", 48.99800, 21.24000),
            ("49.05500, 20.29700", 49.05500, 20.29700),
            ("48.73600, 21.21200", 48.73600, 21.21200),
            ("48.75500, 21.92000", 48.75500, 21.92000),
            ("47.98600, 18.16300", 47.98600, 18.16300),
            ("49.43800, 18.78900", 49.43800, 18.78900),
            ("48.21500, 18.60700", 48.21500, 18.60700),
            ("48.94500, 20.56800", 48.94500, 20.56800)
        };
        var purposes = new[]
        {
            "Hliadkovanie", "Dokumentácia miesta", "Cvičný let", "Kalibrácia senzorov",
            "Monitorovanie davu", "Kontrola hranice", "Prieskum terénu", "Podpora zásahu",
            "Mapovanie", "Výcvik pilota", "Test po údržbe", "Sledovanie vozidla"
        };

        const int diaryFlights = 100;
        var uasCounters = new Dictionary<Guid, (int Count, int Minutes)>();
        for (var i = 0; i < diaryFlights; i++)
        {
            var (pilot, uasId, orgId) = pilots[i % pilots.Count];
            var minutes = 20 + rng.Next(0, 101); // 20–120 min
            var takeoff = now.AddDays(-(i % 120)).AddHours(-(i % 14)).AddMinutes(-(i * 7) % 60);
            var landing = takeoff.AddMinutes(minutes);
            var loc = locations[i % locations.Length];
            db.Flights.Add(new Domain.Flights.Flight
            {
                FlightNumber = $"LET-2026-{(i + 1):0000}",
                Location = loc.Gps,
                LocationLat = loc.Lat,
                LocationLon = loc.Lon,
                LocationRadiusMeters = 800 + rng.Next(0, 1200),
                Purpose = purposes[i % purposes.Length],
                StandaloneReason = "SEED-DIARY",
                OrgUnitId = orgId,
                UasId = uasId,
                PilotId = pilot.Id,
                Status = FlightStatus.Closed,
                TakeoffAt = takeoff,
                LandingAt = landing,
                DurationMinutes = minutes,
                DistanceMeters = 800 + rng.Next(0, 15000),
                MaxAltitudeMeters = 40 + rng.Next(0, 80),
                MaxSpeedMps = 6 + rng.NextDouble() * 10,
                PostFlightNotes = $"Denník letov — {pilot.LicenseNumber}",
                CountersApplied = true,
                ClosedAt = landing
            });
            if (!uasCounters.TryGetValue(uasId, out var c))
                c = (0, 0);
            uasCounters[uasId] = (c.Count + 1, c.Minutes + minutes);

            if (i < pilots.Count)
            {
                pilot.LastFlightAt = landing;
                db.Pilots.Update(pilot);
            }
        }

        await db.SaveChangesAsync();

        foreach (var (uasId, c) in uasCounters)
        {
            var uas = await db.UasAircraft.FirstOrDefaultAsync(u => u.Id == uasId);
            if (uas is null) continue;
            uas.TotalFlightCount = c.Count;
            uas.TotalFlightMinutes = c.Minutes;
        }

        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Demo rozdelenie flotily: časť dronov bez náletu (nepoužívané), ostatné s náletom (používané).
    /// </summary>
    private static async Task EnsureFleetUsageDemoSplitAsync(IsSuasDbContext db)
    {
        var items = await db.UasAircraft
            .Where(u => u.IsActive
                && u.Notes != null
                && u.Notes.StartsWith("SEED-REG-")
                && u.Status != UasLifecycleStatus.Decommissioned)
            .OrderBy(u => u.SerialNumber)
            .ToListAsync();
        if (items.Count == 0) return;

        for (var i = 0; i < items.Count; i++)
        {
            if (i % 4 == 0)
            {
                items[i].TotalFlightCount = 0;
                items[i].TotalFlightMinutes = 0;
            }
            else if (items[i].TotalFlightCount <= 0)
            {
                items[i].TotalFlightCount = 6 + (i % 50);
                items[i].TotalFlightMinutes = items[i].TotalFlightCount * 18;
            }
        }

        await db.SaveChangesAsync();
    }

    /// <summary>40 náhodných porúch na UAS (idempotentné).</summary>
    private static async Task EnsureDemoDefectsAsync(IsSuasDbContext db, Guid reportedByAccountId)
    {
        const int target = 40;
        var existing = await db.Defects.CountAsync(d => d.IsActive && d.DefectTypeCode.StartsWith("SEED-"));
        if (existing >= target)
            return;

        var drones = await db.UasAircraft.AsNoTracking()
            .Where(u => u.IsActive && u.Status != UasLifecycleStatus.Draft)
            .OrderBy(u => u.SerialNumber)
            .ToListAsync();
        if (drones.Count == 0)
            drones = await db.UasAircraft.AsNoTracking().Where(u => u.IsActive).OrderBy(u => u.SerialNumber).ToListAsync();
        if (drones.Count == 0)
            return;

        var titles = new[]
        {
            "Vibrácie vrtule", "Poškodený gimbal", "Slabý GPS signál", "Prehriatie batérie",
            "Chyba RTK fix", "Nefunkčný návrat RTH", "Poškodený podvozok", "Únik oleja z motora",
            "Chyba kompasu", "Zaseknutý zoom objektívu", "Šum na rádiu", "Nesúlad IMU",
            "Poškodená vrtuľa", "Výpadok telemetrie", "Nízka kapacita batérie", "Chyba kalibrácie",
            "Poškodený kryt", "Nestabilný hover", "Chyba senzora prekážok", "Prepätie na napájaní"
        };
        var reports = new[]
        {
            "Počas letu sa vyskytli abnormálne vibrácie; požadovaná kontrola.",
            "Po tvrdom pristátí viditeľné poškodenie; vyradené z prevádzky do posúdenia.",
            "Opakované straty fixácie; let prerušený podľa checklistu.",
            "Teplota prekročila limit podľa telemetrie; batéria izolovaná.",
            "Pilot nahlásil nestabilné správanie pri nízkej výške."
        };
        var types = new[] { "SEED-PROP", "SEED-GIMBAL", "SEED-GPS", "SEED-BAT", "SEED-RADIO", "SEED-STRUCT", "SEED-SENSOR" };
        var severities = new[]
        {
            DefectSeverity.Low, DefectSeverity.Medium, DefectSeverity.Medium,
            DefectSeverity.High, DefectSeverity.Blocking
        };
        var statuses = new[]
        {
            DefectStatus.Reported, DefectStatus.Reported, DefectStatus.UnderRepair,
            DefectStatus.Resolved, DefectStatus.Closed
        };

        var rng = new Random(20260809);
        var next = existing + 1;
        for (var i = next; i <= target; i++)
        {
            var uas = drones[(i - 1) % drones.Count];
            var severity = severities[(i - 1) % severities.Length];
            var status = statuses[(i - 1) % statuses.Length];
            var blocks = severity == DefectSeverity.Blocking || i % 7 == 0;
            var title = titles[(i - 1) % titles.Length] + $" #{i}";
            db.Defects.Add(new Domain.Maintenance.Defect
            {
                TargetType = MaintenanceTargetType.Uas,
                TargetId = uas.Id,
                RelatedUasId = uas.Id,
                Title = title,
                OriginalReport = reports[(i - 1) % reports.Length] + $" ({uas.SerialNumber})",
                OriginalReportLocked = true,
                Severity = severity,
                Status = status,
                BlocksOperations = blocks && status is DefectStatus.Reported or DefectStatus.UnderRepair,
                TemporaryMeasure = blocks ? "Dočasne vyradené z prevádzky" : null,
                ReportedByAccountId = reportedByAccountId,
                DefectTypeCode = types[(i - 1) % types.Length],
                TechnicalAssessment = status is DefectStatus.Resolved or DefectStatus.Closed
                    ? "Seed: technické posúdenie dokončené."
                    : null,
                TestPerformed = status is DefectStatus.Resolved or DefectStatus.Closed,
                ResolutionNotes = status is DefectStatus.Resolved or DefectStatus.Closed
                    ? "Seed: porucha uzavretá."
                    : null,
                ResolvedAt = status is DefectStatus.Resolved or DefectStatus.Closed
                    ? DateTimeOffset.UtcNow.AddDays(-rng.Next(1, 40))
                    : null,
                ResolvedByAccountId = status is DefectStatus.Resolved or DefectStatus.Closed
                    ? reportedByAccountId
                    : null
            });
        }

        await db.SaveChangesAsync();
    }

    /// <summary>Demo operácie do kalendára (idempotentné) — pre koláčové grafy.</summary>
    private static async Task EnsureDemoCalendarOperationsAsync(
        IsSuasDbContext db,
        Guid proposedByAccountId,
        IReadOnlyList<OrgUnit> orgUnits)
    {
        if (orgUnits.Count == 0) return;

        var locations = new (string Label, double Lat, double Lon, int RadiusM)[]
        {
            ("Bratislava — Devín", 48.17380, 16.97820, 1200),
            ("Bratislava — Ivanka", 48.17000, 17.20000, 1000),
            ("Trnava", 48.37740, 17.58830, 1500),
            ("Piešťany", 48.59000, 17.83000, 1100),
            ("Nitra — Zobor", 48.34420, 18.09630, 1400),
            ("Komárno", 47.76300, 18.12800, 1000),
            ("Trenčín", 48.89550, 18.05230, 1300),
            ("Prievidza", 48.77500, 18.62500, 1200),
            ("Žilina", 49.22300, 18.73900, 1500),
            ("Martin", 49.06500, 18.92200, 1100),
            ("Banská Bystrica", 48.73200, 19.14000, 1400),
            ("Zvolen", 48.63900, 19.13900, 1000),
            ("Prešov", 48.99800, 21.24000, 1300),
            ("Poprad", 49.05500, 20.29700, 1200),
            ("Košice", 48.73600, 21.21200, 1500),
            ("Michalovce", 48.75500, 21.92000, 1000)
        };

        var existing = await db.Operations
            .Where(o => o.Description != null && o.Description.StartsWith("SEED-CALENDAR"))
            .OrderBy(o => o.PlannedStart)
            .ToListAsync();
        if (existing.Count > 0)
        {
            for (var i = 0; i < existing.Count; i++)
            {
                var loc = locations[i % locations.Length];
                var op = existing[i];
                if (op.LocationLat is null || op.LocationLon is null)
                {
                    op.LocationAddress = loc.Label;
                    op.LocationLat = loc.Lat;
                    op.LocationLon = loc.Lon;
                    op.Description = $"SEED-CALENDAR\nPolomer oblasti: {loc.RadiusM} m";
                }
            }
            await db.SaveChangesAsync();
            return;
        }

        var titles = new[]
        {
            "Hliadka hranice", "Dokumentácia miesta", "Cvičný let VLOS", "Mapovanie areálu",
            "Podpora zásahu", "Kontrola mosta", "Prieskum terénu", "Výcvik posádky"
        };
        var statuses = new[]
        {
            OperationStatus.Draft, OperationStatus.Submitted, OperationStatus.Approved,
            OperationStatus.Approved, OperationStatus.Returned, OperationStatus.Rejected
        };
        var now = DateTimeOffset.UtcNow;
        // Npgsql timestamptz accepts only UTC (Offset=0)
        var monthStart = new DateTimeOffset(now.Year, now.Month, 1, 8, 0, 0, TimeSpan.Zero);

        for (var i = 0; i < 24; i++)
        {
            var org = orgUnits[i % orgUnits.Count];
            var loc = locations[i % locations.Length];
            var start = monthStart.AddDays(i * 2).AddHours(i % 6);
            var end = start.AddHours(2 + (i % 3));
            db.Operations.Add(new Domain.Operations.Operation
            {
                Title = $"{titles[i % titles.Length]} #{i + 1}",
                OperationTypeCode = "PATROL",
                Status = statuses[i % statuses.Length],
                OrgUnitId = org.Id,
                ProposedByAccountId = proposedByAccountId,
                PlannedStart = start,
                PlannedEnd = end,
                LocationAddress = loc.Label,
                LocationLat = loc.Lat,
                LocationLon = loc.Lon,
                Description = $"SEED-CALENDAR\nPolomer oblasti: {loc.RadiusM} m",
                IsUrgent = i % 5 == 0,
                UrgentReason = i % 5 == 0 ? "Seed: neodkladná aktivita" : null,
                Version = 1
            });
        }

        await db.SaveChangesAsync();
    }

    /// <summary>Demo kritické/high upozornenia na Prehľad (idempotentné).</summary>
    private static async Task EnsureDemoCriticalAlertsAsync(IsSuasDbContext db, Guid recipientAccountId)
    {
        if (await db.AppNotifications.AnyAsync(n => n.RecipientRoleCode == "SEED-CRITICAL"))
            return;

        var items = new (string Title, string Message, string Priority, string TypeCode, string Related)[]
        {
            ("Blokujúca porucha batérie", "SUAS-2026-00007: teplota batérie prekročila limit — vyradené z prevádzky.", "Critical", "DEFECT", "Defect"),
            ("Neodkladná operácia bez schválenia", "Operácia „Podpora zásahu #5“ je neodkladná a čaká na rozhodnutie.", "Critical", "OPERATION", "Operation"),
            ("Vypršaná licencia pilota", "Pilot PIL-SEED-014: platnosť preukazu skončila — nie je spôsobilý do operácie.", "High", "PILOT", "Pilot"),
            ("Konflikt vo vzdušnom priestore", "Plánovaný let v blízkosti CTR LZIB — vyžaduje sa mapová kontrola.", "High", "AIRSPACE", "Operation"),
            ("Pozastavený UAS bez servisu", "SUAS-2026-00022 je pozastavený viac ako 14 dní bez otvorenej servisnej žiadosti.", "Critical", "UAS", "Uas"),
            ("Incident čaká na uzavretie", "Bezpečnostné hlásenie: strata VLOS počas hliadky — chýbajú nápravné opatrenia.", "High", "SAFETY", "SafetyEvent")
        };

        foreach (var item in items)
        {
            db.AppNotifications.Add(new Domain.Safety.AppNotification
            {
                Title = item.Title,
                Message = item.Message,
                TypeCode = item.TypeCode,
                Priority = item.Priority,
                RecipientAccountId = recipientAccountId,
                RecipientRoleCode = "SEED-CRITICAL",
                RelatedEntityType = item.Related,
                Status = NotificationStatus.Unread,
                CreatedBy = recipientAccountId,
                UpdatedBy = recipientAccountId
            });
        }

        await db.SaveChangesAsync();
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
            ("notifications.write", "Zápis notifikácií", "MOD-11"),
            ("geozones.read", "Čítanie GEO zón", "MOD-19"),
            ("geozones.write", "Import GEO zón", "MOD-19")
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
                "notifications.read", "geozones.read"
            ],
            [SystemRole.UnitAdministrator] =
            [
                "orgunits.read", "orgunits.write", "accounts.read", "accounts.write", "roles.assign",
                "lookups.read", "uas.read", "components.read", "components.write",
                "batteries.read", "batteries.write", "pilots.read", "pilots.write", "operations.read",
                "operations.write", "operations.approve", "flights.read", "flights.write", "maintenance.read", "maintenance.write",
                "safety.read", "safety.write", "documents.read", "documents.write", "reports.read",
                "reports.write", "system.info.read", "controls.read",
                "notifications.read", "notifications.write", "geozones.read"
            ],
            [SystemRole.Approver] =
            [
                "lookups.read", "uas.read", "uas.approve", "pilots.read", "operations.read",
                "operations.approve", "flights.read", "safety.read", "documents.read", "reports.read",
                "reports.official", "notifications.read", "geozones.read"
            ],
            [SystemRole.RemotePilot] =
            [
                "orgunits.read", "lookups.read", "uas.read", "batteries.read", "pilots.read",
                "operations.read", "operations.write", "flights.read", "flights.write",
                "maintenance.read", "maintenance.write", "safety.read", "safety.write",
                "documents.read", "documents.write", "reports.read", "notifications.read", "geozones.read"
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

    private static async Task EnsureSqlitePilotCertificateColumnsAsync(IsSuasDbContext db)
    {
        try
        {
            await db.Database.ExecuteSqlRawAsync(
                """ALTER TABLE "PilotCertificates" ADD COLUMN "SuspendedUntil" INTEGER NULL""");
        }
        catch
        {
            // stĺpec už existuje
        }
    }

    private static async Task EnsureAccessLevelsAsync(IsSuasDbContext db)
    {
        var accounts = await db.Accounts.Include(a => a.RoleAssignments).ThenInclude(r => r.Role).ToListAsync();
        foreach (var a in accounts)
        {
            var codes = a.RoleAssignments.Where(r => r.IsActive).Select(r => r.Role.Code).ToHashSet();
            AccessLevel level;
            if (codes.Contains(SystemRole.CentralGestor)
                || codes.Contains(SystemRole.SystemAdministrator)
                || codes.Contains(SystemRole.Approver))
                level = AccessLevel.Level1;
            else if (codes.Contains(SystemRole.UnitAdministrator))
                level = AccessLevel.Level2;
            else if (codes.Contains(SystemRole.RemotePilot))
                level = AccessLevel.Level3;
            else if (codes.Contains(SystemRole.SafetyOfficer) || codes.Contains(SystemRole.TechnicalWorker))
                level = AccessLevel.Level1;
            else
                level = AccessLevel.Level3;

            if (a.AccessLevel != level)
                a.AccessLevel = level;
            if (level == AccessLevel.Level3 && a.LicenseStatus == LicenseStatus.NotApplicable)
                a.LicenseStatus = LicenseStatus.Confirmed;
            if (level != AccessLevel.Level3 && a.LicenseStatus == LicenseStatus.Pending)
                a.LicenseStatus = LicenseStatus.NotApplicable;
        }
        await db.SaveChangesAsync();
    }

    /// <summary>Zabezpečí demo loginy (L2/L3) s pevnými heslami — upsert pri každom štarte.</summary>
    private static async Task EnsureNamedLoginUsersAsync(IsSuasDbContext db, ILogger logger)
    {
        var krBa = await db.OrgUnits.FirstOrDefaultAsync(o => o.Code == "KR-BA");
        if (krBa is null) return;

        await UpsertNamedLoginUserAsync(
            db,
            userName: "martin",
            firstName: "Martin",
            lastName: "Horváth",
            email: "martin.horvath@mv.sk.local",
            password: "3XXAgVWi#MKxQ2bY",
            level: AccessLevel.Level3,
            roleCode: SystemRole.RemotePilot,
            homeOrg: krBa);

        await UpsertNamedLoginUserAsync(
            db,
            userName: "janko.novak",
            firstName: "Ján",
            lastName: "Novák",
            email: "janko.novak@mv.sk.local",
            password: "!tt!DKhENEcE7esB",
            level: AccessLevel.Level2,
            roleCode: SystemRole.UnitAdministrator,
            homeOrg: krBa);

        await db.SaveChangesAsync();
        await EnsureMartinPilotAndFlightsAsync(db, logger);
        await EnsurePilotOpenCategoryCertificatesAsync(db);
        logger.LogInformation("Ensured named login users martin (L3) and janko.novak (L2) under KR-BA.");
    }

    /// <summary>Piloti iba s A1/A3 — karta Register PnD / A1/A3.</summary>
    private static async Task EnsureA1A3OnlyDemoPilotsAsync(IsSuasDbContext db, IReadOnlyList<OrgUnit> krUnits)
    {
        var orgs = krUnits.Count > 0
            ? krUnits.ToList()
            : await db.OrgUnits.AsNoTracking().Where(o => o.IsActive).OrderBy(o => o.Code).Take(20).ToListAsync();
        if (orgs.Count == 0) return;

        const int target = 40;
        var existing = await db.Pilots.CountAsync(p =>
            p.LicenseNumber != null && p.LicenseNumber.StartsWith("PIL-A1ONLY-"));
        if (existing >= target) return;

        var firstNames = new[]
        {
            "Adam", "Peter", "Martin", "Jozef", "Michal", "Tomáš", "Lukáš", "Marek", "Jakub", "Samuel",
            "Filip", "Matej", "Patrik", "Richard", "Ondrej", "Roman", "Erik", "Daniel", "Dominik", "Vladimír",
            "Anna", "Mária", "Eva", "Zuzana", "Katarína", "Jana", "Lucia", "Monika", "Lenka", "Petra"
        };
        var lastNames = new[]
        {
            "Horváth", "Nagy", "Szabó", "Kováč", "Varga", "Tóth", "Molnár", "Baláž", "Lukáč", "Novák",
            "Horváthová", "Kováčová", "Szabová", "Nagyová", "Tóthová", "Balážová", "Vargová", "Molnárová",
            "Urban", "Polák", "Šimko", "Hudák", "Gajdoš", "Černák", "Bartoš", "Oravec", "Krajčí", "Farkaš"
        };

        var now = DateTimeOffset.UtcNow;
        for (var i = existing + 1; i <= target; i++)
        {
            var org = orgs[(i - 1) % orgs.Count];
            var fn = firstNames[(i - 1) % firstNames.Length];
            var ln = lastNames[(i * 5) % lastNames.Length];
            var person = new Person
            {
                FirstName = fn,
                LastName = ln,
                Email = $"pilot.a1a3.{i:000}@mv.sk.local",
                HomeOrgUnitId = org.Id
            };
            db.Persons.Add(person);
            db.Pilots.Add(new Domain.Pilots.Pilot
            {
                Person = person,
                OrgUnitId = org.Id,
                LicenseNumber = $"PIL-A1ONLY-{i:000}",
                Status = PilotStatus.Fit,
                CurrencyValidUntil = now.AddMonths(8),
                PersonalBadgeNumber = $"ČOZ-A1-{i:00000}",
                GestorCertificateNumber = $"GES-A1-{i:00000}",
                LogbookNumber = $"DN-A1-{i:00000}",
                Notes = "SEED-A1ONLY"
            });
        }

        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Doplní PnD preukazy v rebríčku: A1/A3; A1/A3+A2; +STS1; +STS1+STS2.
    /// Zároveň doplní ČOZ / Osv. ges. / č. denníka.
    /// </summary>
    private static async Task EnsurePilotOpenCategoryCertificatesAsync(IsSuasDbContext db)
    {
        await PruneDuplicatePnDCertificatesAsync(db);
        var now = DateTimeOffset.UtcNow;
        var pilots = await db.Pilots
            .Include(p => p.Certificates)
            .Include(p => p.Person)
            .Where(p => p.IsActive)
            .OrderBy(p => p.LicenseNumber)
            .ToListAsync();

        var i = 0;
        foreach (var pilot in pilots)
        {
            i++;
            var key = pilot.LicenseNumber ?? pilot.Id.ToString("N")[..8];
            if (string.IsNullOrWhiteSpace(pilot.PersonalBadgeNumber))
                pilot.PersonalBadgeNumber = $"ČOZ-{i:00000}";
            if (string.IsNullOrWhiteSpace(pilot.GestorCertificateNumber))
                pilot.GestorCertificateNumber = $"GES-{i:00000}";
            if (string.IsNullOrWhiteSpace(pilot.LogbookNumber))
                pilot.LogbookNumber = $"DN-PnD-{i:00000}";

            foreach (var legacy in pilot.Certificates.Where(c =>
                         c.IsActive && string.Equals(c.CertificateTypeCode, "STS", StringComparison.OrdinalIgnoreCase)))
            {
                legacy.CertificateTypeCode = "STS1";
                if (string.IsNullOrWhiteSpace(legacy.TitleSk) || legacy.TitleSk.Contains("STS", StringComparison.OrdinalIgnoreCase))
                    legacy.TitleSk = "Osvedčenie STS1";
            }

            EnsureIssuedCert(db, pilot, "A1_A3", "Preukaz pilota na diaľku — kategória A1/A3",
                $"DU-A1A3-{key}", now);

            var isA1OnlySeed = (pilot.LicenseNumber ?? "").StartsWith("PIL-A1ONLY-", StringComparison.OrdinalIgnoreCase);
            var isMartin = string.Equals(pilot.LicenseNumber, "PIL-MARTIN-001", StringComparison.OrdinalIgnoreCase);
            var wantsA2 = !isA1OnlySeed && (isMartin
                || string.Equals(pilot.LicenseNumber, "PIL-001", StringComparison.OrdinalIgnoreCase)
                || i % 5 == 0
                || i % 5 == 2);
            var wantsSts1 = wantsA2 && !isMartin && (i % 7 == 0 || i % 7 == 3);
            var wantsSts2 = wantsSts1 && i % 11 == 0;

            if (wantsA2)
            {
                EnsureIssuedCert(db, pilot, "A2", "Preukaz pilota na diaľku — kategória A2",
                    $"DU-A2-{key}", now);
            }
            if (wantsSts1)
            {
                var mtow = (i % 2 == 0) ? "MTOW 25 kg" : $"platné do {now.AddYears(1):dd.MM.yyyy}";
                EnsureIssuedCert(db, pilot, "STS1", "Osvedčenie STS1",
                    $"EU-STS1-{key}", now, mtow);
            }
            if (wantsSts2)
            {
                EnsureIssuedCert(db, pilot, "STS2", "Osvedčenie STS2",
                    $"EU-STS2-{key}", now, "MTOW 25 kg");
            }

            DeactivateUnwantedSeedCerts(pilot, wantsA2, wantsSts1, wantsSts2);
        }

        await db.SaveChangesAsync();
    }

    private static void DeactivateUnwantedSeedCerts(Domain.Pilots.Pilot pilot, bool wantsA2, bool wantsSts1, bool wantsSts2)
    {
        var license = pilot.LicenseNumber ?? "";
        var isSeed = license.StartsWith("PIL-SEED-", StringComparison.OrdinalIgnoreCase)
            || license.StartsWith("PIL-A1ONLY-", StringComparison.OrdinalIgnoreCase);
        if (!isSeed) return;

        void Deactivate(string typeCode)
        {
            foreach (var cert in pilot.Certificates.Where(c =>
                         c.IsActive && string.Equals(c.CertificateTypeCode, typeCode, StringComparison.OrdinalIgnoreCase)))
            {
                cert.IsActive = false;
                cert.Status = CertificateStatus.Revoked;
            }
        }

        if (!wantsA2) Deactivate("A2");
        if (!wantsSts1)
        {
            Deactivate("STS1");
            Deactivate("STS");
        }
        if (!wantsSts2) Deactivate("STS2");
    }

    private static void EnsureIssuedCert(
        IsSuasDbContext db,
        Domain.Pilots.Pilot pilot,
        string typeCode,
        string titleSk,
        string externalNumber,
        DateTimeOffset now,
        string? mtowOrValidityText = null)
    {
        var existing = pilot.Certificates
            .Where(c => c.CertificateTypeCode == typeCode
                || (typeCode == "STS1" && (c.CertificateTypeCode == "STS" || c.CertificateTypeCode == "STS01")))
            .OrderByDescending(c => c.IsActive)
            .ThenByDescending(c => c.Status == CertificateStatus.Issued)
            .ThenByDescending(c => c.Version)
            .FirstOrDefault();
        if (existing is not null)
        {
            existing.IsActive = true;
            existing.SuspendedUntil = null;
            if (existing.CertificateTypeCode != typeCode)
                existing.CertificateTypeCode = typeCode;
            if (existing.Status != CertificateStatus.Issued)
                existing.Status = CertificateStatus.Issued;
            if (existing.ValidTo is null || existing.ValidTo < now)
            {
                existing.ValidFrom = now.AddMonths(-2);
                existing.ValidTo = now.AddYears(1);
                existing.IssuedAt ??= now.AddMonths(-2);
            }
            if (string.IsNullOrWhiteSpace(existing.ExternalNumber))
                existing.ExternalNumber = externalNumber;
            if (!string.IsNullOrWhiteSpace(mtowOrValidityText) && string.IsNullOrWhiteSpace(existing.MtowOrValidityText))
                existing.MtowOrValidityText = mtowOrValidityText;
            return;
        }

        db.PilotCertificates.Add(new Domain.Pilots.PilotCertificate
        {
            PilotId = pilot.Id,
            CertificateTypeCode = typeCode,
            TitleSk = titleSk,
            ExternalNumber = externalNumber,
            Version = 1,
            Status = CertificateStatus.Issued,
            IssuedAt = now.AddMonths(-2),
            ValidFrom = now.AddMonths(-2),
            ValidTo = now.AddYears(1),
            MtowOrValidityText = mtowOrValidityText
        });
    }

    private static async Task PruneDuplicatePnDCertificatesAsync(IsSuasDbContext db)
    {
        var codes = new[] { "A1_A3", "A2", "STS1", "STS2", "STS", "A1A3", "OPEN_A1_A3", "OPEN_A2" };
        var all = await db.PilotCertificates
            .Where(c => codes.Contains(c.CertificateTypeCode))
            .ToListAsync();
        if (all.Count == 0) return;

        var doomed = all
            .Where(c => !c.IsActive || c.Status != CertificateStatus.Issued)
            .Select(c => c.Id)
            .ToHashSet();

        foreach (var group in all
                     .Where(c => c.IsActive && c.Status == CertificateStatus.Issued)
                     .GroupBy(c => (c.PilotId, (c.CertificateTypeCode ?? "").Trim().ToUpperInvariant())))
        {
            var keep = group
                .OrderByDescending(c => c.IssuedAt ?? c.CreatedAt)
                .ThenByDescending(c => c.Version)
                .First();
            foreach (var extra in group.Where(c => c.Id != keep.Id))
                doomed.Add(extra.Id);
        }

        if (doomed.Count == 0) return;

        foreach (var cert in all.Where(c => doomed.Contains(c.Id)))
        {
            cert.IsActive = false;
            if (cert.Status == CertificateStatus.Issued)
                cert.Status = CertificateStatus.Superseded;
            cert.SupersedesCertificateId = null;
        }

        await db.SaveChangesAsync();
    }

    private static async Task EnsureMartinPilotAndFlightsAsync(IsSuasDbContext db, ILogger logger)
    {
        var account = await db.Accounts.Include(a => a.Person)
            .FirstOrDefaultAsync(a => a.UserName == "martin");
        if (account is null) return;

        var krBa = await db.OrgUnits.FirstOrDefaultAsync(o => o.Code == "KR-BA");
        var homeOrgId = krBa?.Id
            ?? account.Person.HomeOrgUnitId
            ?? throw new InvalidOperationException("Chýba KR-BA / home org pre Martina.");

        account.Person.HomeOrgUnitId = homeOrgId;

        var pilot = await db.Pilots.FirstOrDefaultAsync(p => p.PersonId == account.PersonId);
        if (pilot is null)
        {
            pilot = new Domain.Pilots.Pilot
            {
                PersonId = account.PersonId,
                OrgUnitId = homeOrgId,
                LicenseNumber = "PIL-MARTIN-001",
                Status = PilotStatus.Fit,
                CurrencyValidUntil = DateTimeOffset.UtcNow.AddMonths(12),
                Notes = "Demo pilot viazaný na účet martin (KR Bratislava)"
            };
            db.Pilots.Add(pilot);
            await db.SaveChangesAsync();
        }
        else
        {
            pilot.OrgUnitId = homeOrgId;
            if (string.IsNullOrWhiteSpace(pilot.LicenseNumber))
                pilot.LicenseNumber = "PIL-MARTIN-001";
            pilot.Status = PilotStatus.Fit;
        }

        // Presuň Martinove lety do KR-BA (aby ich videl L2 správca kraja)
        var martinFlights = await db.Flights
            .Where(f => f.PilotId == pilot.Id || f.StandaloneReason == "SEED-MARTIN")
            .ToListAsync();
        foreach (var f in martinFlights)
        {
            f.OrgUnitId = homeOrgId;
            f.PilotId = pilot.Id;
        }
        await db.SaveChangesAsync();

        if (await db.Flights.AnyAsync(f => f.StandaloneReason == "SEED-MARTIN"))
            return;

        var uas = await db.UasAircraft.AsNoTracking()
            .Where(u => u.IsActive && u.OwningOrgUnitId == homeOrgId)
            .OrderBy(u => u.SerialNumber)
            .FirstOrDefaultAsync()
            ?? await db.UasAircraft.AsNoTracking()
                .Where(u => u.IsActive)
                .OrderBy(u => u.SerialNumber)
                .FirstOrDefaultAsync();

        var spots = new (string Label, double Lat, double Lon, double Radius, string Purpose)[]
        {
            ("Bratislava — Petržalka", 48.1255, 17.1105, 1200, "Hliadkovanie"),
            ("Bratislava — Devín", 48.1738, 16.9782, 800, "Dokumentácia miesta"),
            ("Trnava — centrum", 48.3774, 17.5883, 1000, "Cvičný let"),
            ("Nitra — Zobor", 48.3442, 18.0963, 1500, "Prieskum terénu"),
            ("Trenčín — Brezina", 48.8955, 18.0523, 900, "Mapovanie"),
            ("Žilina — Strečno", 49.1870, 18.8620, 1100, "Kontrola hranice"),
            ("Banská Bystrica — Urpín", 48.7320, 19.1400, 1000, "Monitorovanie davu"),
            ("Zvolen — Sliač", 48.6390, 19.1390, 1300, "Výcvik pilota"),
            ("Prešov — Cemjata", 48.9680, 21.2150, 1000, "Podpora zásahu"),
            ("Košice — Bankov", 48.7360, 21.2120, 1400, "Kalibrácia senzorov")
        };

        var now = DateTimeOffset.UtcNow;
        for (var i = 0; i < spots.Length; i++)
        {
            var s = spots[i];
            var takeoff = now.AddDays(-(i + 1) * 2).AddHours(-(8 + i % 5));
            var landing = takeoff.AddMinutes(25 + i * 7);
            var flight = new Domain.Flights.Flight
            {
                StandaloneReason = "SEED-MARTIN",
                OrgUnitId = homeOrgId,
                UasId = uas?.Id,
                PilotId = pilot.Id,
                FlightNumber = $"LET-MARTIN-{(i + 1):00}",
                Location = $"{s.Lat:0.00000}, {s.Lon:0.00000}",
                LocationLat = s.Lat,
                LocationLon = s.Lon,
                LocationRadiusMeters = s.Radius,
                Purpose = s.Purpose,
                Status = FlightStatus.Closed,
                TakeoffAt = takeoff,
                LandingAt = landing,
                DurationMinutes = (int)(landing - takeoff).TotalMinutes,
                DistanceMeters = s.Radius * 1.6,
                MaxAltitudeMeters = 60 + i * 8,
                MaxSpeedMps = 8 + i * 0.4,
                PostFlightNotes = $"Demo let Martina Horvátha — {s.Label}",
                ChecklistResult = "OK",
                CountersApplied = true,
                ClosedAt = landing.AddMinutes(5)
            };
            db.Flights.Add(flight);
            await db.SaveChangesAsync();

            var pts = new List<Domain.Flights.FlightTrackPoint>();
            for (var t = 0; t < 6; t++)
            {
                var ang = t * (Math.PI / 3.0);
                var dLat = (s.Radius * 0.35 / 111_320.0) * Math.Cos(ang);
                var dLon = (s.Radius * 0.35 / (111_320.0 * Math.Cos(s.Lat * Math.PI / 180.0))) * Math.Sin(ang);
                pts.Add(new Domain.Flights.FlightTrackPoint
                {
                    FlightId = flight.Id,
                    Sequence = t + 1,
                    Latitude = s.Lat + dLat,
                    Longitude = s.Lon + dLon,
                    AltitudeMeters = 40 + t * 5,
                    SpeedMps = 6 + t * 0.5,
                    RecordedAt = takeoff.AddMinutes(t * 4)
                });
            }
            db.FlightTrackPoints.AddRange(pts);
        }

        pilot.LastFlightAt = now.AddDays(-2);
        await db.SaveChangesAsync();
        logger.LogInformation("Seeded 10 GPS flights for martin under KR-BA.");
    }

    private static async Task UpsertNamedLoginUserAsync(
        IsSuasDbContext db,
        string userName,
        string firstName,
        string lastName,
        string email,
        string password,
        AccessLevel level,
        SystemRole roleCode,
        OrgUnit homeOrg)
    {
        var role = await db.Roles.FirstOrDefaultAsync(r => r.Code == roleCode && r.IsActive)
            ?? throw new InvalidOperationException($"Rola {roleCode} chýba v seedi.");

        var account = await db.Accounts
            .Include(a => a.Person)
            .Include(a => a.RoleAssignments)
            .FirstOrDefaultAsync(a => a.UserName == userName);

        if (account is null)
        {
            var person = new Person
            {
                FirstName = firstName,
                LastName = lastName,
                Email = email,
                HomeOrgUnitId = homeOrg.Id
            };
            account = new Account
            {
                Person = person,
                UserName = userName,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
                Status = AccountStatus.Active,
                IsActive = true,
                AccessLevel = level,
                LicenseStatus = level == AccessLevel.Level3 ? LicenseStatus.Confirmed : LicenseStatus.NotApplicable
            };
            db.Persons.Add(person);
            db.Accounts.Add(account);
            await db.SaveChangesAsync();

            db.RoleAssignments.Add(new RoleAssignment
            {
                AccountId = account.Id,
                RoleId = role.Id,
                OrgUnitId = homeOrg.Id,
                ValidFrom = DateTimeOffset.UtcNow.AddYears(-1),
                Reason = "Named login seed"
            });
            return;
        }

        account.Person.FirstName = firstName;
        account.Person.LastName = lastName;
        account.Person.Email = email;
        account.Person.HomeOrgUnitId = homeOrg.Id;
        account.Status = AccountStatus.Active;
        account.IsActive = true;
        account.AccessLevel = level;
        account.LicenseStatus = level == AccessLevel.Level3 ? LicenseStatus.Confirmed : LicenseStatus.NotApplicable;
        account.PasswordHash = BCrypt.Net.BCrypt.HashPassword(password);

        var matchingRole = account.RoleAssignments.FirstOrDefault(r => r.IsActive && r.RoleId == role.Id);
        if (matchingRole is null)
        {
            db.RoleAssignments.Add(new RoleAssignment
            {
                AccountId = account.Id,
                RoleId = role.Id,
                OrgUnitId = homeOrg.Id,
                ValidFrom = DateTimeOffset.UtcNow.AddYears(-1),
                Reason = "Named login seed"
            });
        }
        else if (matchingRole.OrgUnitId != homeOrg.Id)
        {
            matchingRole.OrgUnitId = homeOrg.Id;
            matchingRole.Reason = "Named login seed (org sync)";
        }
    }

    private static async Task EnsureAtlasOrgStructureAsync(IsSuasDbContext db)
    {
        var root = await db.OrgUnits.FirstOrDefaultAsync(o => o.Code == "MVSR");
        if (root is null) return;

        root.Kind = OrgUnitKind.Root;

        var branchPz = await db.OrgUnits.FirstOrDefaultAsync(o => o.Code == "ZLOZKA-PZ");
        if (branchPz is not null)
        {
            branchPz.Kind = OrgUnitKind.BranchPz;
            if (branchPz.Name.Contains("demo", StringComparison.OrdinalIgnoreCase))
                branchPz.Name = "Policajný zbor";
        }

        async Task EnsureBranch(string code, string name, OrgUnitKind kind)
        {
            var existing = await db.OrgUnits.FirstOrDefaultAsync(o => o.Code == code);
            if (existing is not null)
            {
                existing.Kind = kind;
                return;
            }

            var branch = new OrgUnit
            {
                Code = code,
                Name = name,
                Kind = kind,
                ParentId = root.Id,
                Level = 1,
                Path = "/"
            };
            db.OrgUnits.Add(branch);
            await db.SaveChangesAsync();
            branch.Path = $"{root.Path.TrimEnd('/')}/{branch.Id}/";
            await db.SaveChangesAsync();
        }

        await EnsureBranch("ZLOZKA-HAZZ", "Hasičský a záchranný zbor", OrgUnitKind.BranchHazz);
        await EnsureBranch("ZLOZKA-CO", "Civilná ochrana", OrgUnitKind.BranchCo);

        var units = await db.OrgUnits.Where(o =>
            o.Code != "MVSR" && o.Code != "ZLOZKA-PZ" && o.Code != "ZLOZKA-HAZZ" && o.Code != "ZLOZKA-CO").ToListAsync();
        foreach (var u in units)
        {
            if (u.Code.StartsWith("KR-", StringComparison.Ordinal) || u.Code.StartsWith("UTVAR-", StringComparison.Ordinal))
                u.Kind = OrgUnitKind.Unit;
            else if (u.Level >= 2)
                u.Kind = OrgUnitKind.Unit;
        }

        await db.SaveChangesAsync();
    }

    private static async Task EnsurePilotUasAuthorizationsAsync(IsSuasDbContext db)
    {
        var now = DateTimeOffset.UtcNow.AddYears(-1);
        var existingPairs = await db.PilotUasAuthorizations.AsNoTracking()
            .Where(a => a.IsActive)
            .Select(a => new { a.PilotId, a.UasId })
            .ToListAsync();
        var pairSet = existingPairs.Select(x => (x.PilotId, x.UasId)).ToHashSet();

        var pilots = await db.Pilots.AsNoTracking().Where(p => p.IsActive).ToListAsync();
        var drones = await db.UasAircraft.AsNoTracking()
            .Where(u => u.IsActive && u.OwningOrgUnitId != null)
            .ToListAsync();
        var dronesBySerial = drones
            .GroupBy(d => d.SerialNumber, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
        var dronesByOrg = drones
            .GroupBy(d => d.OwningOrgUnitId!.Value)
            .ToDictionary(g => g.Key, g => g.ToList());

        var added = 0;
        foreach (var pilot in pilots)
        {
            Guid? uasId = null;
            Guid? orgId = null;

            if (!string.IsNullOrWhiteSpace(pilot.Notes)
                && pilot.Notes.StartsWith("PrimaryUas:", StringComparison.OrdinalIgnoreCase))
            {
                var serial = pilot.Notes["PrimaryUas:".Length..].Trim();
                if (dronesBySerial.TryGetValue(serial, out var primary)
                    && primary.OwningOrgUnitId == pilot.OrgUnitId)
                {
                    uasId = primary.Id;
                    orgId = pilot.OrgUnitId;
                }
            }

            if (uasId is null && dronesByOrg.TryGetValue(pilot.OrgUnitId, out var orgDrones) && orgDrones.Count > 0)
            {
                var pick = orgDrones[Math.Abs(HashCode.Combine(pilot.Id)) % orgDrones.Count];
                uasId = pick.Id;
                orgId = pilot.OrgUnitId;
            }

            if (uasId is null || orgId is null)
                continue;
            if (pairSet.Contains((pilot.Id, uasId.Value)))
                continue;

            db.PilotUasAuthorizations.Add(new PilotUasAuthorization
            {
                PilotId = pilot.Id,
                UasId = uasId.Value,
                OrgUnitId = orgId.Value,
                ValidFrom = now,
                Note = "Seed oprávnenie pilot×UAS"
            });
            pairSet.Add((pilot.Id, uasId.Value));
            added++;
        }

        if (added > 0)
            await db.SaveChangesAsync();
    }

    private static async Task EnsureDemoMonthlyReportsAsync(IsSuasDbContext db)
    {
        var auths = await db.PilotUasAuthorizations.AsNoTracking()
            .Where(a => a.IsActive)
            .ToListAsync();
        if (auths.Count == 0) return;

        var now = DateTimeOffset.UtcNow;
        var periods = new[]
        {
            (Year: now.AddMonths(-1).Year, Month: now.AddMonths(-1).Month, Status: MonthlyFlightReportStatus.Confirmed),
            (Year: now.Year, Month: now.Month, Status: MonthlyFlightReportStatus.Draft)
        };

        foreach (var (year, month, status) in periods)
        {
            foreach (var group in auths.GroupBy(a => a.OrgUnitId).Take(8))
            {
                var exists = await db.MonthlyFlightReports.AnyAsync(r =>
                    r.IsActive && r.OrgUnitId == group.Key && r.Year == year && r.Month == month);
                if (exists) continue;

                var report = new MonthlyFlightReport
                {
                    OrgUnitId = group.Key,
                    Year = year,
                    Month = month,
                    Status = status,
                    ConfirmedAt = status == MonthlyFlightReportStatus.Confirmed ? now.AddDays(-2) : null,
                    Note = status == MonthlyFlightReportStatus.Confirmed ? "Seed potvrdený výkaz" : "Seed koncept"
                };
                db.MonthlyFlightReports.Add(report);
                await db.SaveChangesAsync();

                var i = 0;
                foreach (var auth in group.Take(status == MonthlyFlightReportStatus.Draft ? 2 : 4))
                {
                    i++;
                    db.MonthlyFlightReportLines.Add(new MonthlyFlightReportLine
                    {
                        ReportId = report.Id,
                        PilotId = auth.PilotId,
                        UasId = auth.UasId,
                        Hours = 2 + (i % 5),
                        Minutes = 15 * (i % 4),
                        Takeoffs = 3 + i,
                        Landings = 3 + i,
                        OperationalFlights = 2,
                        TrainingFlights = 1,
                        OtherFlights = 0,
                        Note = "Seed"
                    });
                }

                await db.SaveChangesAsync();
            }
        }
    }
}
