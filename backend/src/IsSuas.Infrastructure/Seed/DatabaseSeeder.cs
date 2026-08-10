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
                var existingGestor = await db.Accounts.FirstOrDefaultAsync(a => a.UserName == "gestor");
                if (existingGestor is not null)
                {
                    await EnsureDemoDefectsAsync(db, existingGestor.Id);
                    await EnsureDemoCalendarOperationsAsync(db, existingGestor.Id,
                        existingKrs.Count > 0 ? existingKrs : new List<OrgUnit> { existingA, existingB });
                    await EnsureDemoCriticalAlertsAsync(db, existingGestor.Id);
                }
            }
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

        // Demo UAS / komponenty / batérie / KR / piloti / lety
        var krs = await EnsureKrOrgUnitsAsync(db, branchPz);
        var orgPool = krs.Select(k => k.Id).ToList();
        await EnsureDemoFleetVolumeAsync(db, orgPool, unitA.Id, unitB.Id);
        await EnsureDemoPilotsAndFlightsAsync(db, krs);
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
        logger.LogInformation("Seed completed. Demo password: Heslo123!");
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

        var manufacturers = new (string Maker, string Model, string Type)[]
        {
            ("DJI", "M300 RTK", "MULTI_ROTOR"),
            ("DJI", "M30T", "MULTI_ROTOR"),
            ("DJI", "Air 3", "MULTI_ROTOR"),
            ("DJI", "Avata 2", "MULTI_ROTOR"),
            ("Parrot", "Anafi USA", "MULTI_ROTOR"),
            ("Parrot", "Anafi Ai", "MULTI_ROTOR"),
            ("Autel", "Evo Max 4T", "MULTI_ROTOR"),
            ("Autel", "Evo II Dual", "MULTI_ROTOR"),
            ("Skydio", "X2D", "MULTI_ROTOR"),
            ("Quantum", "Vector", "FIXED_WING"),
            ("Wingtra", "WingtraOne GEN II", "FIXED_WING"),
            ("Freefly", "Astro", "MULTI_ROTOR")
        };

        var uasStatuses = new[]
        {
            UasLifecycleStatus.InService, UasLifecycleStatus.InService, UasLifecycleStatus.InService,
            UasLifecycleStatus.InService, UasLifecycleStatus.InMaintenance, UasLifecycleStatus.InStorage,
            UasLifecycleStatus.Suspended, UasLifecycleStatus.Damaged, UasLifecycleStatus.Draft,
            UasLifecycleStatus.Registered
        };

        var existingUas = await db.UasAircraft.CountAsync(x => x.IsActive);
        if (existingUas < target)
        {
            var next = existingUas + 1;
            for (var i = next; i <= target; i++)
            {
                var spec = manufacturers[(i - 1) % manufacturers.Length];
                var status = uasStatuses[(i - 1) % uasStatuses.Length];
                db.UasAircraft.Add(new Domain.Fleet.Uas
                {
                    SerialNumber = $"DEMO-UAS-{i:000}",
                    Manufacturer = spec.Maker,
                    Model = spec.Model,
                    UasTypeCode = spec.Type,
                    RegistrationNumber = status == UasLifecycleStatus.Draft ? null : $"SUAS-2026-{i:00000}",
                    Status = status,
                    OwningOrgUnitId = OrgFor(i),
                    RegisteredAt = status == UasLifecycleStatus.Draft ? null : now.AddDays(-rng.Next(10, 400)),
                    Notes = $"Seed demo UAS {i}",
                    TotalFlightCount = 0,
                    TotalFlightMinutes = 0
                });
            }
            await db.SaveChangesAsync();
        }

        var componentTypes = new (string Code, string Name)[]
        {
            ("GIMBAL", "Gimbal kamera"),
            ("CAMERA", "RGB kamera"),
            ("THERMAL", "Termovízna kamera"),
            ("RTK", "RTK modul"),
            ("PROP", "Vrtuľa sada"),
            ("GPS", "GNSS anténa"),
            ("RADIO", "Rádiový modul"),
            ("LANDING", "Pristávací podvozok"),
            ("SPEAKER", "Megafón"),
            ("SPOT", "Svetlomet"),
            ("PARACHUTE", "Padákový systém"),
            ("SENSOR", "Lidar senzor")
        };

        var assetStatuses = new[]
        {
            AssetUsabilityStatus.Available, AssetUsabilityStatus.Available, AssetUsabilityStatus.Available,
            AssetUsabilityStatus.InUse, AssetUsabilityStatus.Suspended, AssetUsabilityStatus.Damaged
        };

        var existingComp = await db.Components.CountAsync(x => x.IsActive);
        if (existingComp < target)
        {
            var next = existingComp + 1;
            for (var i = next; i <= target; i++)
            {
                var t = componentTypes[(i - 1) % componentTypes.Length];
                db.Components.Add(new Domain.Fleet.Component
                {
                    InternalNumber = $"CMP-{i:000}",
                    SerialNumber = $"SN-CMP-{10000 + i}",
                    Name = $"{t.Name} #{i}",
                    ComponentTypeCode = t.Code,
                    Status = assetStatuses[(i - 1) % assetStatuses.Length],
                    OwningOrgUnitId = OrgFor(i)
                });
            }
        }

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
        if (await db.Flights.AnyAsync(f => f.StandaloneReason == "SEED-VOLUME" || f.StandaloneReason == "SEED-DIARY"))
            return;

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

        // Denník: presne 100 uzavretých letov (pilot lietá len na svojom drone).
        var locations = new[]
        {
            "Bratislava — Devínska Kobyla", "Bratislava — letisko Ivanka", "Trnava — Modranka",
            "Piešťany — cvičný priestor", "Nitra — Zobor", "Komárno — Dunaj",
            "Trenčín — Brezina", "Prievidza — Handlová", "Žilina — Strečno",
            "Martin — Turčianska kotlina", "Banská Bystrica — Urpín", "Zvolen — Sliač",
            "Prešov — Cemjata", "Poprad — Tatry", "Košice — Bankov", "Michalovce — Zemplín",
            "Nové Zámky — poľnohospodársky areál", "Čadca — Kysuce", "Levice — Hron",
            "Spišská Nová Ves — Hornád"
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
            db.Flights.Add(new Domain.Flights.Flight
            {
                FlightNumber = $"LET-2026-{(i + 1):0000}",
                Location = locations[i % locations.Length],
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
        if (await db.Operations.AnyAsync(o => o.Description == "SEED-CALENDAR"))
            return;

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
                LocationAddress = org.Name,
                Description = "SEED-CALENDAR",
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
