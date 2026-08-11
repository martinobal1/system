using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using IsSuas.Application.Auth;
using IsSuas.Application.Accounts;
using IsSuas.Application.Controls;
using IsSuas.Application.Fleet;
using IsSuas.Application.Flights;
using IsSuas.Application.Maintenance;
using IsSuas.Application.Operations;
using IsSuas.Application.OrgUnits;
using IsSuas.Application.Pilots;
using IsSuas.Application.Safety;
using IsSuas.Application.Documents;
using IsSuas.Application.Weather;
using IsSuas.Application.Reporting;
using IsSuas.Application.Lookups;
using IsSuas.Application.Notifications;
using IsSuas.Application.Search;
using IsSuas.Domain.Enums;
using IsSuas.Domain.Reporting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace IsSuas.IntegrationTests;

public class AccScenarioTests : IClassFixture<IsSuasWebApplicationFactory>
{
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public AccScenarioTests(IsSuasWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Acc1_UasDuplicateAndApprove_AssignsRegistrationNumber()
    {
        await LoginAs("gestor");
        var create = await _client.PostAsJsonAsync("/api/v1/uas", new CreateUasRequest(
            "SN-ACC1-001", "DJI", "M300", "MULTI_ROTOR", null, null));
        create.EnsureSuccessStatusCode();
        var uas = await create.Content.ReadFromJsonAsync<UasDto>(JsonOptions);
        Assert.NotNull(uas);

        var dup = await _client.PostAsJsonAsync("/api/v1/uas", new CreateUasRequest(
            "SN-ACC1-001", "DJI", "M300", "MULTI_ROTOR", null, null));
        Assert.Equal(HttpStatusCode.Conflict, dup.StatusCode);

        var submit = await _client.PostAsync($"/api/v1/uas/{uas.Id}/submit", null);
        submit.EnsureSuccessStatusCode();

        await LoginAs("schvalovatel");
        var approve = await _client.PostAsync($"/api/v1/uas/{uas.Id}/approve", null);
        approve.EnsureSuccessStatusCode();
        var approved = await approve.Content.ReadFromJsonAsync<UasDto>(JsonOptions);
        Assert.NotNull(approved);
        Assert.StartsWith("SUAS-", approved.RegistrationNumber);
        Assert.Equal("Registered", approved.Status.ToString());
    }

    [Fact]
    public async Task Acc2_SecondActiveAssignment_IsRejected_HistoryRemains()
    {
        await LoginAs("gestor");
        var orgs = await _client.GetFromJsonAsync<List<OrgUnitDto>>("/api/v1/org-units", JsonOptions);
        Assert.NotNull(orgs);
        var unitA = orgs.First(o => o.Code == "UTVAR-A");
        var unitB = orgs.First(o => o.Code == "UTVAR-B");

        var create = await _client.PostAsJsonAsync("/api/v1/uas", new CreateUasRequest(
            "SN-ACC2-001", "Autel", "Evo", null, unitA.Id, null));
        create.EnsureSuccessStatusCode();
        var uas = await create.Content.ReadFromJsonAsync<UasDto>(JsonOptions);
        Assert.NotNull(uas);

        var first = await _client.PostAsJsonAsync($"/api/v1/uas/{uas.Id}/assignments",
            new AssignUasRequest(unitA.Id, DateTimeOffset.UtcNow, "Prvé pridelenie"));
        first.EnsureSuccessStatusCode();

        var second = await _client.PostAsJsonAsync($"/api/v1/uas/{uas.Id}/assignments",
            new AssignUasRequest(unitB.Id, DateTimeOffset.UtcNow.AddMinutes(-1), "Konflikt"));
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);

        var detail = await _client.GetFromJsonAsync<UasDetailDto>($"/api/v1/uas/{uas.Id}", JsonOptions);
        Assert.NotNull(detail);
        Assert.NotEmpty(detail.Assignments);
    }

    [Fact]
    public async Task Acc13_UnitUser_CannotOpenForeignOrgUas()
    {
        await LoginAs("gestor");
        var orgs = await _client.GetFromJsonAsync<List<OrgUnitDto>>("/api/v1/org-units", JsonOptions);
        Assert.NotNull(orgs);
        var unitB = orgs.First(o => o.Code == "UTVAR-B");

        var create = await _client.PostAsJsonAsync("/api/v1/uas", new CreateUasRequest(
            "SN-ACC13-001", "Parrot", "Anafi", null, unitB.Id, null));
        create.EnsureSuccessStatusCode();
        var uas = await create.Content.ReadFromJsonAsync<UasDto>(JsonOptions);
        Assert.NotNull(uas);

        await LoginAs("spravca.a");
        var forbidden = await _client.GetAsync($"/api/v1/uas/{uas.Id}");
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
    }

    [Fact]
    public async Task Acc16_DeactivatedAccount_CannotLogin_ButAuthorshipRemainsVisible()
    {
        await LoginAs("gestor");
        var orgs = await _client.GetFromJsonAsync<List<OrgUnitDto>>("/api/v1/org-units", JsonOptions);
        var unitA = orgs!.First(o => o.Code == "UTVAR-A");
        var accounts = await _client.GetFromJsonAsync<List<AccountDto>>("/api/v1/accounts", JsonOptions);
        var spravca = accounts!.First(a => a.UserName == "spravca.a");
        var spravcaRoles = await _client.GetFromJsonAsync<List<RoleAssignmentDto>>(
            $"/api/v1/accounts/{spravca.Id}/role-assignments", JsonOptions);
        var roleId = spravcaRoles!.First(r => r.RoleCode == nameof(SystemRole.UnitAdministrator)).RoleId;

        var userName = "author." + Guid.NewGuid().ToString("N")[..8];
        var createAcc = await _client.PostAsJsonAsync("/api/v1/accounts", new CreateAccountRequest(
            userName, "Autor", "Historicky", null, unitA.Id, AccessLevel.Level2, "Heslo123!"));
        createAcc.EnsureSuccessStatusCode();
        var createdWrap = await createAcc.Content.ReadFromJsonAsync<AccountCreatedDto>(JsonOptions);
        Assert.NotNull(createdWrap);
        var account = createdWrap.Account;

        var assign = await _client.PostAsJsonAsync($"/api/v1/accounts/{account.Id}/role-assignments", new CreateRoleAssignmentRequest(
            roleId, unitA.Id, DateTimeOffset.UtcNow.AddDays(-1), null, "Acc16"));
        assign.EnsureSuccessStatusCode();

        await LoginAs(userName);
        var uasRes = await _client.PostAsJsonAsync("/api/v1/uas", new CreateUasRequest(
            "SN-ACC16-" + Guid.NewGuid().ToString("N")[..6], "DJI", "Air", null, unitA.Id, null));
        uasRes.EnsureSuccessStatusCode();
        var uas = await uasRes.Content.ReadFromJsonAsync<UasDto>(JsonOptions);
        Assert.NotNull(uas);

        await LoginAs("gestor");
        accounts = await _client.GetFromJsonAsync<List<AccountDto>>("/api/v1/accounts", JsonOptions);
        account = accounts!.First(a => a.Id == account.Id);
        var deactivate = await _client.PatchAsJsonAsync($"/api/v1/accounts/{account.Id}/status",
            new ChangeAccountStatusRequest(AccountStatus.Deactivated, "Acc16 deaktivácia", account.RowVersion));
        deactivate.EnsureSuccessStatusCode();

        _client.DefaultRequestHeaders.Authorization = null;
        var loginFail = await _client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(userName, "Heslo123!"));
        Assert.Equal(HttpStatusCode.Unauthorized, loginFail.StatusCode);

        await LoginAs("gestor");
        var stillListed = await _client.GetFromJsonAsync<List<AccountDto>>("/api/v1/accounts", JsonOptions);
        Assert.NotNull(stillListed);
        var deactivated = stillListed.First(a => a.Id == account.Id);
        Assert.False(deactivated.IsActive);
        Assert.Equal("Autor", deactivated.FirstName);
        Assert.Equal("Historicky", deactivated.LastName);

        var stillUas = await _client.GetAsync($"/api/v1/uas/{uas.Id}");
        stillUas.EnsureSuccessStatusCode();

        var audit = await _client.GetFromJsonAsync<List<AuditEventDto>>(
            $"/api/v1/audit-events?actorUserName={userName}&entityId={uas.Id}", JsonOptions);
        Assert.NotNull(audit);
        Assert.Contains(audit, e =>
            e.ActorUserName == userName
            && e.Action.Contains("Uas", StringComparison.OrdinalIgnoreCase)
            && e.EntityId == uas.Id);
    }

    [Fact]
    public async Task Acc12_BlockingChecklistItem_PreventsCompletion()
    {
        await LoginAs("gestor");
        var templates = await _client.GetFromJsonAsync<List<FormTemplateDto>>("/api/v1/form-templates", JsonOptions);
        Assert.NotNull(templates);
        var template = templates.First(t => t.Code == "PRE_OP_CHECKLIST");

        var start = await _client.PostAsJsonAsync("/api/v1/form-submissions", new StartFormSubmissionRequest(
            template.Id, LinkedEntityType.Operation, Guid.NewGuid()));
        start.EnsureSuccessStatusCode();
        var submission = await start.Content.ReadFromJsonAsync<FormSubmissionDto>(JsonOptions);
        Assert.NotNull(submission);
        Assert.True(submission.HasBlockingIncomplete);

        var emptyComplete = await _client.PostAsJsonAsync($"/api/v1/form-submissions/{submission.Id}/answers",
            new SaveFormAnswersRequest([], true, submission.RowVersion));
        Assert.Equal(HttpStatusCode.BadRequest, emptyComplete.StatusCode);

        var yesItem = template.Items.First(i => i.Code == "WEATHER_OK");
        var airItem = template.Items.First(i => i.Code == "AIRSPACE_OK");
        // Explicit false on blocking YesNo must also block completion
        var falseAnswer = await _client.PostAsJsonAsync($"/api/v1/form-submissions/{submission.Id}/answers",
            new SaveFormAnswersRequest([
                new FormAnswerDto(yesItem.Id, yesItem.Code, null, false, null, null),
                new FormAnswerDto(airItem.Id, airItem.Code, null, true, null, null)
            ], true, submission.RowVersion));
        Assert.Equal(HttpStatusCode.BadRequest, falseAnswer.StatusCode);
    }

    [Fact]
    public async Task Acc16_DeactivatedAccount_CannotLogin()
    {
        _client.DefaultRequestHeaders.Authorization = null;
        var response = await _client.PostAsJsonAsync("/api/v1/auth/login",
            new LoginRequest("neaktivny", "Heslo123!"));
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Acc3_EligiblePilot_CanBeAssigned_UnfitIsBlocked()
    {
        await LoginAs("gestor");
        var pilots = await _client.GetFromJsonAsync<List<PilotDto>>("/api/v1/pilots", JsonOptions);
        Assert.NotNull(pilots);

        var fit = pilots.First(p => p.LicenseNumber == "PIL-001");
        var unfit = pilots.First(p => p.LicenseNumber == "PIL-002");

        var fitCheck = await _client.GetFromJsonAsync<PilotEligibilityCheckDto>($"/api/v1/pilots/{fit.Id}/eligibility", JsonOptions);
        var unfitCheck = await _client.GetFromJsonAsync<PilotEligibilityCheckDto>($"/api/v1/pilots/{unfit.Id}/eligibility", JsonOptions);

        Assert.NotNull(fitCheck);
        Assert.NotNull(unfitCheck);
        Assert.True(fitCheck.IsEligible);
        Assert.False(unfitCheck.IsEligible);
    }

    [Fact]
    public async Task Acc4_Operation_Draft_Submit_Approve()
    {
        await LoginAs("gestor");
        var orgs = await _client.GetFromJsonAsync<List<OrgUnitDto>>("/api/v1/org-units", JsonOptions);
        var pilots = await _client.GetFromJsonAsync<List<PilotDto>>("/api/v1/pilots", JsonOptions);
        Assert.NotNull(orgs);
        Assert.NotNull(pilots);
        var unitA = orgs.First(o => o.Code == "UTVAR-A");
        var fitPilot = pilots.First(p => p.LicenseNumber == "PIL-001");

        var uasCreate = await _client.PostAsJsonAsync("/api/v1/uas", new CreateUasRequest(
            "SN-ACC4-001", "DJI", "M300", null, unitA.Id, null));
        uasCreate.EnsureSuccessStatusCode();
        var uas = await uasCreate.Content.ReadFromJsonAsync<UasDto>(JsonOptions);
        Assert.NotNull(uas);
        await _client.PostAsync($"/api/v1/uas/{uas.Id}/submit", null);
        await LoginAs("schvalovatel");
        var approvedUas = await (await _client.PostAsync($"/api/v1/uas/{uas.Id}/approve", null))
            .Content.ReadFromJsonAsync<UasDto>(JsonOptions);
        Assert.NotNull(approvedUas);

        await LoginAs("gestor");
        var start = DateTimeOffset.UtcNow.AddDays(1);
        var createOp = await _client.PostAsJsonAsync("/api/v1/operations", new CreateOperationRequest(
            "ACC4 hliadka", "PATROL", unitA.Id, start, start.AddHours(2),
            "Bratislava", 48.14, 17.10, null, "Test", false, null));
        createOp.EnsureSuccessStatusCode();
        var op = await createOp.Content.ReadFromJsonAsync<OperationDto>(JsonOptions);
        Assert.NotNull(op);

        (await _client.PostAsJsonAsync($"/api/v1/operations/{op.Id}/resources",
            new AssignOperationResourceRequest(OperationResourceType.Uas, approvedUas.Id, null))).EnsureSuccessStatusCode();
        (await _client.PostAsJsonAsync($"/api/v1/operations/{op.Id}/resources",
            new AssignOperationResourceRequest(OperationResourceType.Pilot, fitPilot.Id, "REMOTE_PILOT"))).EnsureSuccessStatusCode();

        var submitted = await (await _client.PostAsJsonAsync($"/api/v1/operations/{op.Id}/submit", new { rowVersion = op.RowVersion }))
            .Content.ReadFromJsonAsync<OperationDto>(JsonOptions);
        Assert.NotNull(submitted);
        Assert.Equal(OperationStatus.Submitted, submitted.Status);

        await LoginAs("schvalovatel");
        var approved = await (await _client.PostAsJsonAsync($"/api/v1/operations/{submitted.Id}/approve",
            new DecisionRequest("Schválené ACC4", submitted.RowVersion)))
            .Content.ReadFromJsonAsync<OperationDto>(JsonOptions);
        Assert.NotNull(approved);
        Assert.Equal(OperationStatus.Approved, approved.Status);
    }

    [Fact]
    public async Task Acc5_UrgentOperation_CreatesFollowUpTask()
    {
        await LoginAs("gestor");
        var orgs = await _client.GetFromJsonAsync<List<OrgUnitDto>>("/api/v1/org-units", JsonOptions);
        Assert.NotNull(orgs);
        var unitA = orgs.First(o => o.Code == "UTVAR-A");
        var start = DateTimeOffset.UtcNow.AddHours(1);

        var create = await _client.PostAsJsonAsync("/api/v1/operations", new CreateOperationRequest(
            "Neodkladna ACC5", "URGENT", unitA.Id, start, start.AddHours(1),
            null, null, null, null, null, true, "Havarijna situacia"));
        create.EnsureSuccessStatusCode();

        var tasks = await _client.GetFromJsonAsync<List<WorkTaskDto>>("/api/v1/work-tasks", JsonOptions);
        Assert.NotNull(tasks);
        Assert.Contains(tasks, t => t.TaskTypeCode == "URGENT_OPERATION_FOLLOWUP" && t.Title.Contains("Neodkladna ACC5"));
    }

    [Fact]
    public async Task Acc14_Approver_CannotApproveOwnOperation()
    {
        await LoginAs("gestor");
        var orgs = await _client.GetFromJsonAsync<List<OrgUnitDto>>("/api/v1/org-units", JsonOptions);
        var pilots = await _client.GetFromJsonAsync<List<PilotDto>>("/api/v1/pilots", JsonOptions);
        Assert.NotNull(orgs);
        Assert.NotNull(pilots);
        var unitA = orgs.First(o => o.Code == "UTVAR-A");
        var fitPilot = pilots.First(p => p.LicenseNumber == "PIL-001");

        var uasCreate = await _client.PostAsJsonAsync("/api/v1/uas", new CreateUasRequest(
            "SN-ACC14-001", "Autel", "Evo", null, unitA.Id, null));
        var uas = await uasCreate.Content.ReadFromJsonAsync<UasDto>(JsonOptions);
        Assert.NotNull(uas);
        await _client.PostAsync($"/api/v1/uas/{uas.Id}/submit", null);
        await LoginAs("schvalovatel");
        uas = await (await _client.PostAsync($"/api/v1/uas/{uas.Id}/approve", null)).Content.ReadFromJsonAsync<UasDto>(JsonOptions);
        Assert.NotNull(uas);

        await LoginAs("gestor");
        var start = DateTimeOffset.UtcNow.AddDays(3);
        var createOp = await _client.PostAsJsonAsync("/api/v1/operations", new CreateOperationRequest(
            "ACC14 vlastny navrh", "PATROL", unitA.Id, start, start.AddHours(1),
            null, null, null, null, null, false, null));
        createOp.EnsureSuccessStatusCode();
        var op = await createOp.Content.ReadFromJsonAsync<OperationDto>(JsonOptions);
        Assert.NotNull(op);

        (await _client.PostAsJsonAsync($"/api/v1/operations/{op.Id}/resources",
            new AssignOperationResourceRequest(OperationResourceType.Uas, uas.Id, null))).EnsureSuccessStatusCode();
        (await _client.PostAsJsonAsync($"/api/v1/operations/{op.Id}/resources",
            new AssignOperationResourceRequest(OperationResourceType.Pilot, fitPilot.Id, "REMOTE_PILOT"))).EnsureSuccessStatusCode();

        var submitted = await (await _client.PostAsJsonAsync($"/api/v1/operations/{op.Id}/submit", new { rowVersion = op.RowVersion }))
            .Content.ReadFromJsonAsync<OperationDto>(JsonOptions);
        Assert.NotNull(submitted);

        // Same user (gestor) tries to approve own proposal -> SoD 403
        var approveOwn = await _client.PostAsJsonAsync($"/api/v1/operations/{submitted.Id}/approve",
            new DecisionRequest("Pokus o vlastne schvalenie", submitted.RowVersion));
        Assert.Equal(HttpStatusCode.Forbidden, approveOwn.StatusCode);
    }

    [Fact]
    public async Task Acc6_TelemetryImport_IsIdempotent_AndCreatesTrack()
    {
        await LoginAs("gestor");
        var orgs = await _client.GetFromJsonAsync<List<OrgUnitDto>>("/api/v1/org-units", JsonOptions);
        Assert.NotNull(orgs);
        var unitA = orgs.First(o => o.Code == "UTVAR-A");

        var flightCreate = await _client.PostAsJsonAsync("/api/v1/flights/standalone", new CreateStandaloneFlightRequest(
            unitA.Id, "ACC6 test", null, null, null,
            DateTimeOffset.UtcNow.AddHours(-1), DateTimeOffset.UtcNow));
        flightCreate.EnsureSuccessStatusCode();
        var flight = await flightCreate.Content.ReadFromJsonAsync<FlightDto>(JsonOptions);
        Assert.NotNull(flight);

        var payload = """
            {"points":[
              {"lat":48.14,"lon":17.10,"alt":120,"time":"2026-08-03T10:00:00Z"},
              {"lat":48.15,"lon":17.11,"alt":130,"time":"2026-08-03T10:05:00Z"},
              {"lat":48.16,"lon":17.12,"alt":110,"time":"2026-08-03T10:10:00Z"}
            ]}
            """;

        var first = await _client.PostAsJsonAsync("/api/v1/telemetry-imports", new ImportTelemetryRequest(
            "acc6.json", "json", payload, flight.Id, null, null, null, null));
        first.EnsureSuccessStatusCode();
        var import1 = await first.Content.ReadFromJsonAsync<TelemetryImportDto>(JsonOptions);
        Assert.NotNull(import1);
        Assert.Equal(TelemetryImportStatus.Processed, import1.Status);

        var second = await _client.PostAsJsonAsync("/api/v1/telemetry-imports", new ImportTelemetryRequest(
            "acc6.json", "json", payload, flight.Id, null, null, null, null));
        second.EnsureSuccessStatusCode();
        var import2 = await second.Content.ReadFromJsonAsync<TelemetryImportDto>(JsonOptions);
        Assert.NotNull(import2);
        Assert.Equal(TelemetryImportStatus.DuplicateIgnored, import2.Status);

        var detail = await _client.GetFromJsonAsync<FlightDetailDto>($"/api/v1/flights/{flight.Id}", JsonOptions);
        Assert.NotNull(detail);
        Assert.Equal(3, detail.TrackPoints.Count);
        Assert.False(string.IsNullOrWhiteSpace(detail.TrackGeoJson));
    }

    [Fact]
    public async Task Acc7_CloseFlight_AppliesCountersExactlyOnce()
    {
        await LoginAs("gestor");
        var orgs = await _client.GetFromJsonAsync<List<OrgUnitDto>>("/api/v1/org-units", JsonOptions);
        var pilots = await _client.GetFromJsonAsync<List<PilotDto>>("/api/v1/pilots", JsonOptions);
        Assert.NotNull(orgs);
        Assert.NotNull(pilots);
        var unitA = orgs.First(o => o.Code == "UTVAR-A");
        var fitPilot = pilots.First(p => p.LicenseNumber == "PIL-001");

        var uasCreate = await _client.PostAsJsonAsync("/api/v1/uas", new CreateUasRequest(
            "SN-ACC7-001", "DJI", "M300", null, unitA.Id, null));
        var uas = await uasCreate.Content.ReadFromJsonAsync<UasDto>(JsonOptions);
        Assert.NotNull(uas);
        await _client.PostAsync($"/api/v1/uas/{uas.Id}/submit", null);
        await LoginAs("schvalovatel");
        uas = await (await _client.PostAsync($"/api/v1/uas/{uas.Id}/approve", null)).Content.ReadFromJsonAsync<UasDto>(JsonOptions);
        Assert.NotNull(uas);

        await LoginAs("gestor");
        var batteryCreate = await _client.PostAsJsonAsync("/api/v1/batteries", new CreateBatteryRequest(
            "BAT-ACC7", "BSN-ACC7", "DJI", "TB60", null, unitA.Id));
        batteryCreate.EnsureSuccessStatusCode();
        var battery = await batteryCreate.Content.ReadFromJsonAsync<BatteryDto>(JsonOptions);
        Assert.NotNull(battery);

        var takeoff = DateTimeOffset.UtcNow.AddMinutes(-40);
        var landing = DateTimeOffset.UtcNow.AddMinutes(-10);
        var flightCreate = await _client.PostAsJsonAsync("/api/v1/flights/standalone", new CreateStandaloneFlightRequest(
            unitA.Id, "ACC7 close", uas.Id, fitPilot.Id, battery.Id, takeoff, landing));
        flightCreate.EnsureSuccessStatusCode();
        var flight = await flightCreate.Content.ReadFromJsonAsync<FlightDto>(JsonOptions);
        Assert.NotNull(flight);

        await _client.PostAsJsonAsync($"/api/v1/flights/{flight.Id}/ready", new { rowVersion = flight.RowVersion });
        var detailBeforeClose = await _client.GetFromJsonAsync<FlightDetailDto>($"/api/v1/flights/{flight.Id}", JsonOptions);
        Assert.NotNull(detailBeforeClose);

        var closed = await (await _client.PostAsJsonAsync($"/api/v1/flights/{flight.Id}/close", new CloseFlightRequest(
            "Bez odchylok", "OK", null, detailBeforeClose.Flight.RowVersion)))
            .Content.ReadFromJsonAsync<FlightDto>(JsonOptions);
        Assert.NotNull(closed);
        Assert.True(closed.CountersApplied);
        Assert.Equal(FlightStatus.Closed, closed.Status);

        var uasAfter = await _client.GetFromJsonAsync<UasDetailDto>($"/api/v1/uas/{uas.Id}", JsonOptions);
        Assert.NotNull(uasAfter);
        Assert.Equal(1, uasAfter.Uas.TotalFlightCount);
        Assert.True(uasAfter.Uas.TotalFlightMinutes >= 1);

        var batteries = await _client.GetFromJsonAsync<List<BatteryDto>>("/api/v1/batteries", JsonOptions);
        Assert.NotNull(batteries);
        Assert.Equal(1, batteries.First(b => b.Id == battery.Id).CycleCount);

        var secondClose = await _client.PostAsJsonAsync($"/api/v1/flights/{flight.Id}/close", new CloseFlightRequest(
            "Znova", "OK", null, closed.RowVersion));
        Assert.Equal(HttpStatusCode.BadRequest, secondClose.StatusCode);

        uasAfter = await _client.GetFromJsonAsync<UasDetailDto>($"/api/v1/uas/{uas.Id}", JsonOptions);
        Assert.NotNull(uasAfter);
        Assert.Equal(1, uasAfter.Uas.TotalFlightCount);
    }

    [Fact]
    public async Task Acc8_OverdueMaintenance_BlocksUas_UntilCompleted()
    {
        await LoginAs("gestor");
        var orgs = await _client.GetFromJsonAsync<List<OrgUnitDto>>("/api/v1/org-units", JsonOptions);
        Assert.NotNull(orgs);
        var unitA = orgs.First(o => o.Code == "UTVAR-A");

        var uasCreate = await _client.PostAsJsonAsync("/api/v1/uas", new CreateUasRequest(
            "SN-ACC8-001", "DJI", "M300", null, unitA.Id, null));
        var uas = await uasCreate.Content.ReadFromJsonAsync<UasDto>(JsonOptions);
        Assert.NotNull(uas);
        await _client.PostAsync($"/api/v1/uas/{uas.Id}/submit", null);
        await LoginAs("schvalovatel");
        uas = await (await _client.PostAsync($"/api/v1/uas/{uas.Id}/approve", null)).Content.ReadFromJsonAsync<UasDto>(JsonOptions);
        Assert.NotNull(uas);
        await LoginAs("gestor");

        // Put UAS in service so maintenance block can suspend to InMaintenance
        await _client.PostAsJsonAsync($"/api/v1/uas/{uas.Id}/transition", new TransitionUasRequest(
            UasLifecycleStatus.InService, "Do prevadzky", uas.RowVersion));
        uas = (await _client.GetFromJsonAsync<UasDetailDto>($"/api/v1/uas/{uas.Id}", JsonOptions))!.Uas;

        var planCreate = await _client.PostAsJsonAsync("/api/v1/maintenance-plans", new CreateMaintenancePlanRequest(
            "ACC8 po kazdom lete", MaintenanceTargetType.Uas, uas.Id, null,
            MaintenanceTriggerType.FlightCount, 1, null, 1, 80, true));
        planCreate.EnsureSuccessStatusCode();
        var plan = await planCreate.Content.ReadFromJsonAsync<MaintenancePlanDto>(JsonOptions);
        Assert.NotNull(plan);

        var flightCreate = await _client.PostAsJsonAsync("/api/v1/flights/standalone", new CreateStandaloneFlightRequest(
            unitA.Id, "ACC8", uas.Id, null, null,
            DateTimeOffset.UtcNow.AddHours(-1), DateTimeOffset.UtcNow));
        var flight = await flightCreate.Content.ReadFromJsonAsync<FlightDto>(JsonOptions);
        Assert.NotNull(flight);
        await _client.PostAsJsonAsync($"/api/v1/flights/{flight.Id}/ready", new { rowVersion = flight.RowVersion });
        flight = (await _client.GetFromJsonAsync<FlightDetailDto>($"/api/v1/flights/{flight.Id}", JsonOptions))!.Flight;
        (await _client.PostAsJsonAsync($"/api/v1/flights/{flight.Id}/close", new CloseFlightRequest(
            "OK", "OK", null, flight.RowVersion))).EnsureSuccessStatusCode();

        var block = await _client.GetFromJsonAsync<AssetBlockCheckDto>($"/api/v1/uas/{uas.Id}/block-status", JsonOptions);
        Assert.NotNull(block);
        Assert.True(block.IsBlocked);

        var woCreate = await _client.PostAsJsonAsync("/api/v1/work-orders", new CreateWorkOrderRequest(
            plan.Id, MaintenanceTargetType.Uas, uas.Id, "Obnova ACC8", null, null, null));
        var wo = await woCreate.Content.ReadFromJsonAsync<WorkOrderDto>(JsonOptions);
        Assert.NotNull(wo);

        (await _client.PostAsJsonAsync($"/api/v1/work-orders/{wo.Id}/complete", new CompleteWorkOrderRequest(
            "Kontrola", "V poriadku", DateTimeOffset.UtcNow, null, null, true, wo.RowVersion)))
            .EnsureSuccessStatusCode();

        block = await _client.GetFromJsonAsync<AssetBlockCheckDto>($"/api/v1/uas/{uas.Id}/block-status", JsonOptions);
        Assert.NotNull(block);
        Assert.False(block.IsBlocked);
    }

    [Fact]
    public async Task Acc9_DefectOriginalReport_RemainsImmutable()
    {
        await LoginAs("gestor");
        var login = await _client.GetFromJsonAsync<List<AccountDto>>("/api/v1/accounts", JsonOptions);
        Assert.NotNull(login);
        var gestor = login.First(a => a.UserName == "gestor");
        var tech = login.First(a => a.UserName == "spravca.a");

        var orgs = await _client.GetFromJsonAsync<List<OrgUnitDto>>("/api/v1/org-units", JsonOptions);
        var unitA = orgs!.First(o => o.Code == "UTVAR-A");
        var uasCreate = await _client.PostAsJsonAsync("/api/v1/uas", new CreateUasRequest(
            "SN-ACC9-001", "Parrot", "Anafi", null, unitA.Id, null));
        var uas = await uasCreate.Content.ReadFromJsonAsync<UasDto>(JsonOptions);
        Assert.NotNull(uas);

        const string original = "Hlasenie: neobvykle vibracie pri vznase";
        var report = await _client.PostAsJsonAsync("/api/v1/defects", new ReportDefectRequest(
            MaintenanceTargetType.Uas, uas.Id, "Vibracie", original, DefectSeverity.Blocking, true,
            "Dočasne vyradene", null, uas.Id, "VIBRATION"));
        report.EnsureSuccessStatusCode();
        var defect = await report.Content.ReadFromJsonAsync<DefectDto>(JsonOptions);
        Assert.NotNull(defect);
        Assert.True(defect.OriginalReportLocked);
        Assert.Equal(original, defect.OriginalReport);

        var resolve = await _client.PostAsJsonAsync($"/api/v1/defects/{defect.Id}/resolve", new ResolveDefectRequest(
            "Vymena motorov", "Posudenie OK", true, tech.Id, defect.RowVersion));
        resolve.EnsureSuccessStatusCode();
        var resolved = await resolve.Content.ReadFromJsonAsync<DefectDto>(JsonOptions);
        Assert.NotNull(resolved);
        Assert.Equal(original, resolved.OriginalReport);
        Assert.Equal(DefectStatus.Resolved, resolved.Status);
        Assert.False(resolved.BlocksOperations);
        _ = gestor; // reporter identity retained in audit/history
    }

    [Fact]
    public async Task Acc10_CriticalIncident_AlertMeasuresEvidenceControlledClose()
    {
        await LoginAs("gestor");
        var accounts = await _client.GetFromJsonAsync<List<AccountDto>>("/api/v1/accounts", JsonOptions);
        Assert.NotNull(accounts);
        var owner = accounts.First(a => a.UserName == "spravca.a");

        var orgs = await _client.GetFromJsonAsync<List<OrgUnitDto>>("/api/v1/org-units", JsonOptions);
        var unitA = orgs!.First(o => o.Code == "UTVAR-A");

        var uasCreate = await _client.PostAsJsonAsync("/api/v1/uas", new CreateUasRequest(
            "SN-ACC10-001", "DJI", "Avata", null, unitA.Id, null));
        var uas = await uasCreate.Content.ReadFromJsonAsync<UasDto>(JsonOptions);
        Assert.NotNull(uas);
        await _client.PostAsync($"/api/v1/uas/{uas.Id}/submit", null);
        await LoginAs("schvalovatel");
        uas = await (await _client.PostAsync($"/api/v1/uas/{uas.Id}/approve", null)).Content.ReadFromJsonAsync<UasDto>(JsonOptions);
        Assert.NotNull(uas);
        await LoginAs("gestor");
        await _client.PostAsJsonAsync($"/api/v1/uas/{uas.Id}/transition", new TransitionUasRequest(
            UasLifecycleStatus.InService, "Do prevadzky", uas.RowVersion));
        uas = (await _client.GetFromJsonAsync<UasDetailDto>($"/api/v1/uas/{uas.Id}", JsonOptions))!.Uas;

        const string initial = "Strata spojenia nad obydlenou zonou — minimalne hlasenie";
        var create = await _client.PostAsJsonAsync("/api/v1/safety-events", new CreateSafetyEventRequest(
            "ACC10 kriticka strata spojenia", initial, SafetySeverity.Critical, DateTimeOffset.UtcNow,
            unitA.Id, null, null, uas.Id, "Bratislava"));
        create.EnsureSuccessStatusCode();
        var safety = await create.Content.ReadFromJsonAsync<SafetyEventDto>(JsonOptions);
        Assert.NotNull(safety);
        Assert.False(safety.InitialReportLocked);
        Assert.Equal(initial, safety.InitialReport);

        var submit = await _client.PostAsJsonAsync($"/api/v1/safety-events/{safety.Id}/submit",
            new SubmitSafetyEventRequest(safety.RowVersion));
        submit.EnsureSuccessStatusCode();
        safety = await submit.Content.ReadFromJsonAsync<SafetyEventDto>(JsonOptions);
        Assert.NotNull(safety);
        Assert.True(safety.InitialReportLocked);
        Assert.True(safety.AutoSuspendApplied);
        Assert.Equal(SafetyEventStatus.Submitted, safety.Status);

        uas = (await _client.GetFromJsonAsync<UasDetailDto>($"/api/v1/uas/{uas.Id}", JsonOptions))!.Uas;
        Assert.Equal(UasLifecycleStatus.Suspended, uas.Status);

        var notifications = await _client.GetFromJsonAsync<List<AppNotificationDto>>("/api/v1/notifications", JsonOptions);
        Assert.NotNull(notifications);
        Assert.Contains(notifications, n => n.TypeCode == "SAFETY_CRITICAL" && n.RelatedSafetyEventId == safety.Id);

        var evidence = await _client.PostAsJsonAsync($"/api/v1/safety-events/{safety.Id}/evidences", new AddEvidenceRequest(
            "photo-link.jpg", "abc123hashsha256", "image/jpeg", "Foto miesta", false, null));
        evidence.EnsureSuccessStatusCode();
        var ev = await evidence.Content.ReadFromJsonAsync<SafetyEvidenceDto>(JsonOptions);
        Assert.NotNull(ev);
        Assert.Equal(1, ev.Version);
        Assert.True(ev.IsCurrent);

        var v2 = await _client.PostAsJsonAsync($"/api/v1/safety-events/{safety.Id}/evidences", new AddEvidenceRequest(
            "photo-link-v2.jpg", "def456hashsha256", "image/jpeg", "Opravena foto", false, ev.Id));
        v2.EnsureSuccessStatusCode();
        var ev2 = await v2.Content.ReadFromJsonAsync<SafetyEvidenceDto>(JsonOptions);
        Assert.NotNull(ev2);
        Assert.Equal(2, ev2.Version);

        safety = (await _client.GetFromJsonAsync<SafetyEventDetailDto>($"/api/v1/safety-events/{safety.Id}", JsonOptions))!.Event;
        await _client.PostAsJsonAsync($"/api/v1/safety-events/{safety.Id}/investigation", new UpdateInvestigationRequest(
            "Strata RC linku", "Interference", "Mestske prostredie", "Update firmware + trening", safety.RowVersion));

        var actionCreate = await _client.PostAsJsonAsync($"/api/v1/safety-events/{safety.Id}/actions",
            new CreateCorrectiveActionRequest("Aktualizacia firmware", null, owner.Id, DateTimeOffset.UtcNow.AddDays(7), true));
        actionCreate.EnsureSuccessStatusCode();
        var action = await actionCreate.Content.ReadFromJsonAsync<CorrectiveActionDto>(JsonOptions);
        Assert.NotNull(action);

        safety = (await _client.GetFromJsonAsync<SafetyEventDetailDto>($"/api/v1/safety-events/{safety.Id}", JsonOptions))!.Event;
        var closeBlocked = await _client.PostAsJsonAsync($"/api/v1/safety-events/{safety.Id}/close",
            new CloseSafetyEventRequest("Predcasne", null, safety.RowVersion));
        Assert.Equal(HttpStatusCode.BadRequest, closeBlocked.StatusCode);

        await LoginAs("spravca.a");
        (await _client.PostAsJsonAsync($"/api/v1/corrective-actions/{action.Id}/complete",
            new CompleteCorrectiveActionRequest("Firmware 1.2.3 nasadeny", action.RowVersion))).EnsureSuccessStatusCode();
        action = (await _client.GetFromJsonAsync<SafetyEventDetailDto>($"/api/v1/safety-events/{safety.Id}", JsonOptions))!
            .Actions.First(a => a.Id == action.Id);

        var selfVerify = await _client.PostAsJsonAsync($"/api/v1/corrective-actions/{action.Id}/verify",
            new VerifyCorrectiveActionRequest(action.RowVersion));
        Assert.Equal(HttpStatusCode.BadRequest, selfVerify.StatusCode);

        await LoginAs("bezpecnost");
        (await _client.PostAsJsonAsync($"/api/v1/corrective-actions/{action.Id}/verify",
            new VerifyCorrectiveActionRequest(action.RowVersion))).EnsureSuccessStatusCode();

        safety = (await _client.GetFromJsonAsync<SafetyEventDetailDto>($"/api/v1/safety-events/{safety.Id}", JsonOptions))!.Event;
        Assert.Equal(initial, safety.InitialReport);

        var close = await _client.PostAsJsonAsync($"/api/v1/safety-events/{safety.Id}/close",
            new CloseSafetyEventRequest("Opatrenia overene, riziko akceptovane", null, safety.RowVersion));
        close.EnsureSuccessStatusCode();
        safety = await close.Content.ReadFromJsonAsync<SafetyEventDto>(JsonOptions);
        Assert.NotNull(safety);
        Assert.Equal(SafetyEventStatus.Closed, safety.Status);
        Assert.Equal(initial, safety.InitialReport);

        uas = (await _client.GetFromJsonAsync<UasDetailDto>($"/api/v1/uas/{uas.Id}", JsonOptions))!.Uas;
        Assert.Equal(UasLifecycleStatus.InService, uas.Status);
    }

    [Fact]
    public async Task Acc11_DocumentVersioning_AndExpirationNotification()
    {
        await LoginAs("gestor");
        var orgs = await _client.GetFromJsonAsync<List<OrgUnitDto>>("/api/v1/org-units", JsonOptions);
        var unitA = orgs!.First(o => o.Code == "UTVAR-A");
        var uasCreate = await _client.PostAsJsonAsync("/api/v1/uas", new CreateUasRequest(
            "SN-ACC11-001", "DJI", "Mini", null, unitA.Id, null));
        var uas = await uasCreate.Content.ReadFromJsonAsync<UasDto>(JsonOptions);
        Assert.NotNull(uas);

        var v1Res = await _client.PostAsJsonAsync("/api/v1/documents", new UploadDocumentRequest(
            "Poistenie UAS", "INSURANCE", LinkedEntityType.Uas, uas.Id,
            "poistenie-v1.pdf", "application/pdf", 1024, "hash-v1-aaa",
            DocumentSensitivity.Internal, DateTimeOffset.UtcNow.AddMonths(-1), DateTimeOffset.UtcNow.AddDays(30),
            true, true, null, null));
        v1Res.EnsureSuccessStatusCode();
        var v1 = await v1Res.Content.ReadFromJsonAsync<ManagedDocumentDto>(JsonOptions);
        Assert.NotNull(v1);
        Assert.Equal(1, v1.Version);
        Assert.True(v1.IsCurrent);

        var v2Res = await _client.PostAsJsonAsync("/api/v1/documents", new UploadDocumentRequest(
            "Poistenie UAS", "INSURANCE", LinkedEntityType.Uas, uas.Id,
            "poistenie-v2.pdf", "application/pdf", 2048, "hash-v2-bbb",
            DocumentSensitivity.Internal, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddYears(1),
            true, true, v1.Id, "Obnovene"));
        v2Res.EnsureSuccessStatusCode();
        var v2 = await v2Res.Content.ReadFromJsonAsync<ManagedDocumentDto>(JsonOptions);
        Assert.NotNull(v2);
        Assert.Equal(2, v2.Version);
        Assert.True(v2.IsCurrent);
        Assert.Equal(v1.DocumentFamilyId, v2.DocumentFamilyId);

        var all = await _client.GetFromJsonAsync<List<ManagedDocumentDto>>(
            $"/api/v1/documents?entityType=Uas&entityId={uas.Id}", JsonOptions);
        Assert.NotNull(all);
        var old = all.First(d => d.Id == v1.Id);
        Assert.False(old.IsCurrent);
        Assert.Equal(DocumentStatus.Superseded, old.Status);
        Assert.Equal(1, old.Version);

        // Expired document -> upload with past ValidTo creates notification
        var expiredUpload = await _client.PostAsJsonAsync("/api/v1/documents", new UploadDocumentRequest(
            "Osvedcenie exspirovane", "CERT", LinkedEntityType.Uas, uas.Id,
            "cert-old.pdf", "application/pdf", 500, "hash-exp",
            DocumentSensitivity.Internal, DateTimeOffset.UtcNow.AddYears(-2), DateTimeOffset.UtcNow.AddDays(-1),
            false, false, null, null));
        expiredUpload.EnsureSuccessStatusCode();
        var expiredDoc = await expiredUpload.Content.ReadFromJsonAsync<ManagedDocumentDto>(JsonOptions);
        Assert.NotNull(expiredDoc);
        Assert.Equal(DocumentStatus.Expired, expiredDoc.Status);

        // Force-expire a current short-lived doc via refresh
        var shortLived = await (await _client.PostAsJsonAsync("/api/v1/documents", new UploadDocumentRequest(
            "Doc kratka platnost", "TEMP", LinkedEntityType.Uas, uas.Id,
            "temp.pdf", "application/pdf", 100, "hash-temp",
            DocumentSensitivity.Internal, DateTimeOffset.UtcNow.AddDays(-10), DateTimeOffset.UtcNow.AddMilliseconds(50),
            false, false, null, null))).Content.ReadFromJsonAsync<ManagedDocumentDto>(JsonOptions);
        Assert.NotNull(shortLived);
        await Task.Delay(80);
        var refresh = await _client.PostAsync("/api/v1/documents/refresh-expirations", null);
        refresh.EnsureSuccessStatusCode();

        var notes = await _client.GetFromJsonAsync<List<AppNotificationDto>>("/api/v1/notifications", JsonOptions);
        Assert.NotNull(notes);
        Assert.Contains(notes, n => n.TypeCode == "DOCUMENT_EXPIRED");

        // Checklist incomplete blocks completion
        var templates = await _client.GetFromJsonAsync<List<FormTemplateDto>>("/api/v1/form-templates", JsonOptions);
        Assert.NotNull(templates);
        var template = templates.First(t => t.Code == "PRE_OP_CHECKLIST");
        var start = await _client.PostAsJsonAsync("/api/v1/form-submissions", new StartFormSubmissionRequest(
            template.Id, LinkedEntityType.Operation, Guid.NewGuid()));
        start.EnsureSuccessStatusCode();
        var submission = await start.Content.ReadFromJsonAsync<FormSubmissionDto>(JsonOptions);
        Assert.NotNull(submission);
        Assert.True(submission.HasBlockingIncomplete);

        var completeFail = await _client.PostAsJsonAsync($"/api/v1/form-submissions/{submission.Id}/answers",
            new SaveFormAnswersRequest([], true, submission.RowVersion));
        Assert.Equal(HttpStatusCode.BadRequest, completeFail.StatusCode);

        var yesItem = template.Items.First(i => i.Code == "WEATHER_OK");
        var airItem = template.Items.First(i => i.Code == "AIRSPACE_OK");
        var save = await _client.PostAsJsonAsync($"/api/v1/form-submissions/{submission.Id}/answers",
            new SaveFormAnswersRequest([
                new FormAnswerDto(yesItem.Id, yesItem.Code, null, true, null, null),
                new FormAnswerDto(airItem.Id, airItem.Code, null, true, null, null)
            ], true, submission.RowVersion));
        save.EnsureSuccessStatusCode();
        submission = await save.Content.ReadFromJsonAsync<FormSubmissionDto>(JsonOptions);
        Assert.NotNull(submission);
        Assert.Equal(FormSubmissionStatus.Completed, submission.Status);
        Assert.Equal(template.Version, submission.TemplateVersion);
        Assert.True(submission.IsComplete);

        var risk = await _client.PostAsJsonAsync("/api/v1/risk-assessments", new CreateRiskAssessmentRequest(
            LinkedEntityType.Operation, submission.LinkedEntityId, 4, 4, "ACC11", submission.Id));
        risk.EnsureSuccessStatusCode();
        var riskDto = await risk.Content.ReadFromJsonAsync<RiskAssessmentDto>(JsonOptions);
        Assert.NotNull(riskDto);
        Assert.Equal(16, riskDto.Score);
        Assert.Equal(RiskClassification.Critical, riskDto.Classification);
    }

    [Fact]
    public async Task Acc15_OfficialReport_FrozenAfterSourceDataChanges()
    {
        await LoginAs("gestor");
        var live1 = await (await _client.PostAsJsonAsync("/api/v1/reports/generate",
            new ReportRequest("UAS", null, null, null))).Content.ReadFromJsonAsync<ReportDto>(JsonOptions);
        Assert.NotNull(live1);
        var total1 = live1.Totals["uas_total"];

        var create = await _client.PostAsJsonAsync("/api/v1/official-reports",
            new CreateOfficialReportRequest("UAS", "ACC15 oficalny UAS", null, null, null, null));
        create.EnsureSuccessStatusCode();
        var official = await create.Content.ReadFromJsonAsync<OfficialReportDto>(JsonOptions);
        Assert.NotNull(official);
        Assert.Equal(OfficialReportStatus.Draft, official.Status);
        Assert.Contains("uas_total", official.SnapshotJson);

        using (var snapDoc = JsonDocument.Parse(official.SnapshotJson))
        {
            var frozen = snapDoc.RootElement.GetProperty("totals").GetProperty("uas_total").GetDecimal();
            Assert.Equal(total1, frozen);
        }

        var orgs = await _client.GetFromJsonAsync<List<OrgUnitDto>>("/api/v1/org-units", JsonOptions);
        var unitA = orgs!.First(o => o.Code == "UTVAR-A");
        (await _client.PostAsJsonAsync("/api/v1/uas", new CreateUasRequest(
            "SN-ACC15-NEW", "DJI", "Air3", null, unitA.Id, null))).EnsureSuccessStatusCode();

        var live2 = await (await _client.PostAsJsonAsync("/api/v1/reports/generate",
            new ReportRequest("UAS", null, null, null))).Content.ReadFromJsonAsync<ReportDto>(JsonOptions);
        Assert.NotNull(live2);
        Assert.True(live2.Totals["uas_total"] > total1);

        var still = await _client.GetFromJsonAsync<OfficialReportDto>($"/api/v1/official-reports/{official.Id}", JsonOptions);
        Assert.NotNull(still);
        Assert.Equal(official.SnapshotJson, still.SnapshotJson);

        await LoginAs("schvalovatel");
        var approve = await _client.PostAsJsonAsync($"/api/v1/official-reports/{official.Id}/approve",
            new ApproveOfficialReportRequest("Schvalenie ACC15", official.RowVersion));
        approve.EnsureSuccessStatusCode();
        official = await approve.Content.ReadFromJsonAsync<OfficialReportDto>(JsonOptions);
        Assert.NotNull(official);
        Assert.Equal(OfficialReportStatus.Approved, official.Status);
        Assert.Equal(still.SnapshotJson, official.SnapshotJson);

        // Correction = new version; original stays
        await LoginAs("gestor");
        var v2 = await (await _client.PostAsJsonAsync("/api/v1/official-reports",
            new CreateOfficialReportRequest("UAS", "ACC15 oprava", null, null, null, official.Id)))
            .Content.ReadFromJsonAsync<OfficialReportDto>(JsonOptions);
        Assert.NotNull(v2);
        Assert.Equal(2, v2.Version);
        Assert.Equal(official.ReportFamilyId, v2.ReportFamilyId);

        var original = await _client.GetFromJsonAsync<OfficialReportDto>($"/api/v1/official-reports/{official.Id}", JsonOptions);
        Assert.NotNull(original);
        Assert.Equal(still.SnapshotJson, original.SnapshotJson);
    }

    [Fact]
    public async Task AccAdmin_LookupDeactivate_ConfigVersion_AuditExport()
    {
        await LoginAs("gestor");

        var createCode = await _client.PostAsJsonAsync("/api/v1/lookups",
            new CreateLookupCodeRequest("ACC_ADMIN_TYPE", "ACC admin typ", null));
        createCode.EnsureSuccessStatusCode();

        var addItem = await _client.PostAsJsonAsync("/api/v1/lookups/ACC_ADMIN_TYPE/items",
            new CreateLookupItemRequest("ITEM_A", "Polozka A", 1, null));
        addItem.EnsureSuccessStatusCode();
        var item = await addItem.Content.ReadFromJsonAsync<LookupItemDto>(JsonOptions);
        Assert.NotNull(item);
        Assert.True(item.IsActive);

        var deactivate = await _client.PostAsJsonAsync($"/api/v1/lookups/items/{item.Id}/deactivate",
            new DeactivateLookupItemRequest(item.RowVersion));
        deactivate.EnsureSuccessStatusCode();
        var deactivated = await deactivate.Content.ReadFromJsonAsync<LookupItemDto>(JsonOptions);
        Assert.NotNull(deactivated);
        Assert.False(deactivated.IsActive);

        // Item still exists (not deleted)
        var code = await _client.GetFromJsonAsync<LookupCodeDto>("/api/v1/lookups/ACC_ADMIN_TYPE", JsonOptions);
        Assert.NotNull(code);
        Assert.Contains(code.Items, i => i.Id == item.Id && !i.IsActive);

        var v1 = await _client.PostAsJsonAsync("/api/v1/config-rules",
            new CreateConfigRuleRequest("acc.limit", "{\"max\":10}", DateTimeOffset.UtcNow.AddDays(-1), "v1"));
        v1.EnsureSuccessStatusCode();
        var rule1 = await v1.Content.ReadFromJsonAsync<ConfigRuleDto>(JsonOptions);
        Assert.NotNull(rule1);
        Assert.Equal(1, rule1.Version);

        var v2 = await _client.PostAsJsonAsync("/api/v1/config-rules",
            new CreateConfigRuleRequest("acc.limit", "{\"max\":20}", DateTimeOffset.UtcNow, "v2"));
        v2.EnsureSuccessStatusCode();
        var rule2 = await v2.Content.ReadFromJsonAsync<ConfigRuleDto>(JsonOptions);
        Assert.NotNull(rule2);
        Assert.Equal(2, rule2.Version);

        var effectivePast = await _client.GetFromJsonAsync<ConfigRuleDto>(
            $"/api/v1/config-rules/effective/acc.limit?at={Uri.EscapeDataString(DateTimeOffset.UtcNow.AddHours(-12).ToString("o"))}",
            JsonOptions);
        Assert.NotNull(effectivePast);
        Assert.Equal(1, effectivePast.Version);
        Assert.Contains("10", effectivePast.ValueJson);

        var export = await _client.GetAsync("/api/v1/audit-events/export.csv?action=LookupItem.Deactivate");
        export.EnsureSuccessStatusCode();
        var csv = await export.Content.ReadAsStringAsync();
        Assert.Contains("LookupItem.Deactivate", csv);

        var info = await _client.GetFromJsonAsync<SystemInfoDto>("/api/v1/system/info", JsonOptions);
        Assert.NotNull(info);
        Assert.False(string.IsNullOrWhiteSpace(info.AppVersion));

        var connectors = await _client.GetFromJsonAsync<List<IntegrationConnectorDto>>("/api/v1/integrations", JsonOptions);
        Assert.NotNull(connectors);
        Assert.DoesNotContain(connectors, c => c.SecretRef != null && c.SecretRef.Contains("sk_live", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Acc22_ControlAudit_CreateFindingReportAndClose()
    {
        await LoginAs("gestor");
        var orgs = await _client.GetFromJsonAsync<List<OrgUnitDto>>("/api/v1/org-units", JsonOptions);
        var unitA = orgs!.First(o => o.Code == "UTVAR-A");

        var perms = await _client.GetFromJsonAsync<List<PermissionDto>>("/api/v1/permissions", JsonOptions);
        Assert.NotNull(perms);
        Assert.Contains(perms, p => p.Code == "controls.write");

        var create = await _client.PostAsJsonAsync("/api/v1/control-audits", new CreateControlAuditRequest(
            "ACC22 kontrola útvaru A",
            ControlAuditType.InternalControl,
            unitA.Id,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow.AddDays(7),
            "Rozsah: evidencia UAS",
            [
                new CreateChecklistItemRequest("REG", "Je register UAS aktuálny?", 1),
                new CreateChecklistItemRequest("DOC", "Sú povinné dokumenty platné?", 2)
            ]));
        create.EnsureSuccessStatusCode();
        var audit = await create.Content.ReadFromJsonAsync<ControlAuditDto>(JsonOptions);
        Assert.NotNull(audit);
        Assert.Equal(ControlAuditStatus.Planned, audit.Status);

        var started = await (await _client.PostAsJsonAsync($"/api/v1/control-audits/{audit.Id}/start",
            new StartControlAuditRequest(audit.RowVersion))).Content.ReadFromJsonAsync<ControlAuditDto>(JsonOptions);
        Assert.NotNull(started);
        Assert.Equal(ControlAuditStatus.InProgress, started.Status);

        var detail = await _client.GetFromJsonAsync<ControlAuditDetailDto>($"/api/v1/control-audits/{audit.Id}", JsonOptions);
        Assert.NotNull(detail);
        Assert.NotEmpty(detail.Checklist);
        var item = detail.Checklist.First();
        var checklist = await _client.PostAsJsonAsync(
            $"/api/v1/control-audits/{audit.Id}/checklist/{item.Id}",
            new UpdateChecklistResultRequest(ControlCheckpointResultStatus.NonCompliant, "Chýba doklad", item.RowVersion));
        checklist.EnsureSuccessStatusCode();

        var findingRes = await _client.PostAsJsonAsync($"/api/v1/control-audits/{audit.Id}/findings",
            new AddControlFindingRequest("Neúplná dokumentácia", "Chýbajú povinné prílohy", ControlFindingSeverity.High, "Uas", null));
        findingRes.EnsureSuccessStatusCode();
        var finding = await findingRes.Content.ReadFromJsonAsync<ControlFindingDto>(JsonOptions);
        Assert.NotNull(finding);

        var actionRes = await _client.PostAsJsonAsync($"/api/v1/control-audits/findings/{finding.Id}/actions",
            new AddControlActionRequest("Doplniť dokumenty", "Do 14 dní", null, DateTimeOffset.UtcNow.AddDays(14)));
        actionRes.EnsureSuccessStatusCode();
        var action = await actionRes.Content.ReadFromJsonAsync<ControlCorrectiveActionDto>(JsonOptions);
        Assert.NotNull(action);

        var complete = await _client.PostAsJsonAsync($"/api/v1/control-audits/actions/{action.Id}/complete",
            new CompleteControlActionRequest("Doplnené", action.RowVersion));
        complete.EnsureSuccessStatusCode();

        detail = await _client.GetFromJsonAsync<ControlAuditDetailDto>($"/api/v1/control-audits/{audit.Id}", JsonOptions);
        Assert.NotNull(detail);
        var report = await (await _client.PostAsJsonAsync($"/api/v1/control-audits/{audit.Id}/reports",
            new IssueControlReportRequest(null, detail.Audit.RowVersion))).Content.ReadFromJsonAsync<ControlReportDto>(JsonOptions);
        Assert.NotNull(report);
        Assert.True(report.IsFrozen);
        Assert.Equal(1, report.Version);

        detail = await _client.GetFromJsonAsync<ControlAuditDetailDto>($"/api/v1/control-audits/{audit.Id}", JsonOptions);
        var closed = await (await _client.PostAsJsonAsync($"/api/v1/control-audits/{audit.Id}/close",
            new CloseControlAuditRequest("Uzavreté ACC22", detail!.Audit.RowVersion)))
            .Content.ReadFromJsonAsync<ControlAuditDto>(JsonOptions);
        Assert.NotNull(closed);
        Assert.Equal(ControlAuditStatus.Closed, closed.Status);

        var sessions = await _client.GetFromJsonAsync<List<UserSessionDto>>("/api/v1/user-sessions/me", JsonOptions);
        Assert.NotNull(sessions);
        Assert.NotEmpty(sessions);
    }

    [Fact]
    public async Task Acc23_DocumentBinaryStorage_UploadDownloadIntegrity()
    {
        await LoginAs("gestor");
        var orgs = await _client.GetFromJsonAsync<List<OrgUnitDto>>("/api/v1/org-units", JsonOptions);
        var unitA = orgs!.First(o => o.Code == "UTVAR-A");
        var uas = await (await _client.PostAsJsonAsync("/api/v1/uas", new CreateUasRequest(
            "SN-ACC23-001", "DJI", "Mini3", null, unitA.Id, null))).Content.ReadFromJsonAsync<UasDto>(JsonOptions);
        Assert.NotNull(uas);

        var rules = await _client.GetFromJsonAsync<List<RetentionRuleDto>>("/api/v1/retention-rules", JsonOptions);
        Assert.NotNull(rules);
        Assert.Contains(rules, r => r.Code == "DEFAULT_5Y");
        var rule = rules.First(r => r.Code == "DEFAULT_5Y");

        var payload = System.Text.Encoding.UTF8.GetBytes("ACC23-binary-content-v1");
        var hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(payload)).ToLowerInvariant();
        var upload = await _client.PostAsJsonAsync("/api/v1/documents", new UploadDocumentRequest(
            "ACC23 binárny doklad", "MANUAL", LinkedEntityType.Uas, uas.Id,
            "acc23.txt", "text/plain", payload.Length, hash,
            DocumentSensitivity.Internal, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddYears(1),
            false, false, null, null, Convert.ToBase64String(payload), rule.Id));
        upload.EnsureSuccessStatusCode();
        var doc = await upload.Content.ReadFromJsonAsync<ManagedDocumentDto>(JsonOptions);
        Assert.NotNull(doc);
        Assert.True(doc.HasStoredContent);
        Assert.NotNull(doc.StoredFileId);
        Assert.Equal(hash, doc.ContentHash);
        Assert.Equal(rule.Id, doc.RetentionRuleId);

        var download = await _client.GetAsync($"/api/v1/documents/{doc.Id}/content");
        download.EnsureSuccessStatusCode();
        var bytes = await download.Content.ReadAsByteArrayAsync();
        Assert.Equal(payload, bytes);

        var dossier = await (await _client.PostAsJsonAsync("/api/v1/document-dossiers",
            new CreateDocumentDossierRequest("Spis ACC23", "Test", LinkedEntityType.Uas, uas.Id)))
            .Content.ReadFromJsonAsync<DocumentDossierDto>(JsonOptions);
        Assert.NotNull(dossier);
        var withDoc = await (await _client.PostAsJsonAsync($"/api/v1/document-dossiers/{dossier.Id}/documents",
            new AddDossierDocumentRequest(doc.Id))).Content.ReadFromJsonAsync<DocumentDossierDto>(JsonOptions);
        Assert.NotNull(withDoc);
        Assert.Contains(doc.Id, withDoc.DocumentIds);
    }

    [Fact]
    public async Task Acc24_Notifications_RecipientsDeliveryTemplatesAndTaskHistory()
    {
        await LoginAs("gestor");

        var templates = await _client.GetFromJsonAsync<List<MessageTemplateDto>>("/api/v1/message-templates", JsonOptions);
        Assert.NotNull(templates);
        Assert.Contains(templates, t => t.Code == "SAFETY_CRITICAL");
        Assert.Contains(templates, t => t.Code == "DOCUMENT_EXPIRED");

        var rules = await _client.GetFromJsonAsync<List<EscalationRuleDto>>("/api/v1/escalation-rules", JsonOptions);
        Assert.NotNull(rules);
        Assert.Contains(rules, r => r.Code == "UNREAD_24H");

        var publish = await _client.PostAsJsonAsync("/api/v1/notifications", new PublishNotificationRequest(
            "SAFETY_CRITICAL",
            "ACC24 test",
            "Správa ACC24",
            "ACC24_TEST",
            "High",
            null,
            nameof(SystemRole.CentralGestor),
            null,
            "Test",
            null,
            NotificationChannel.InApp));
        publish.EnsureSuccessStatusCode();
        var note = await publish.Content.ReadFromJsonAsync<NotificationDetailDto>(JsonOptions);
        Assert.NotNull(note);
        Assert.NotEmpty(note.Recipients);
        Assert.Contains(note.DeliveryAttempts, d => d.Channel == NotificationChannel.InApp && d.Status == DeliveryAttemptStatus.Sent);

        var detail = await _client.GetFromJsonAsync<NotificationDetailDto>($"/api/v1/notifications/{note.Id}", JsonOptions);
        Assert.NotNull(detail);
        Assert.Equal(note.Id, detail.Id);

        var mark = await _client.PostAsJsonAsync($"/api/v1/notifications/{note.Id}/mark-read",
            new MarkNotificationReadRequest(detail.RowVersion));
        mark.EnsureSuccessStatusCode();
        var read = await mark.Content.ReadFromJsonAsync<NotificationDetailDto>(JsonOptions);
        Assert.NotNull(read);
        Assert.Equal(NotificationStatus.Read, read.Status);

        var orgs = await _client.GetFromJsonAsync<List<OrgUnitDto>>("/api/v1/org-units", JsonOptions);
        var unitA = orgs!.First(o => o.Code == "UTVAR-A");
        var start = DateTimeOffset.UtcNow.AddHours(2);
        var createOp = await _client.PostAsJsonAsync("/api/v1/operations", new CreateOperationRequest(
            "ACC24 neodkladna", "URGENT", unitA.Id, start, start.AddHours(1),
            null, null, null, null, null, true, "Test eskalacie ulohy"));
        createOp.EnsureSuccessStatusCode();

        var tasks = await _client.GetFromJsonAsync<List<WorkTaskDto>>("/api/v1/work-tasks", JsonOptions);
        Assert.NotNull(tasks);
        var task = tasks.First(t => t.Title.Contains("ACC24 neodkladna"));
        var historyCreate = await _client.GetFromJsonAsync<List<WorkTaskHistoryDto>>(
            $"/api/v1/work-tasks/{task.Id}/history", JsonOptions);
        Assert.NotNull(historyCreate);
        Assert.Contains(historyCreate, h => h.ToStatus == WorkTaskStatus.Open && h.FromStatus is null);

        var status = await _client.PostAsJsonAsync($"/api/v1/work-tasks/{task.Id}/status",
            new UpdateWorkTaskStatusRequest(WorkTaskStatus.InProgress, "ACC24 progress", task.RowVersion));
        status.EnsureSuccessStatusCode();

        var history = await _client.GetFromJsonAsync<List<WorkTaskHistoryDto>>(
            $"/api/v1/work-tasks/{task.Id}/history", JsonOptions);
        Assert.NotNull(history);
        Assert.Contains(history, h => h.FromStatus == WorkTaskStatus.Open && h.ToStatus == WorkTaskStatus.InProgress);

        var esc = await _client.PostAsync("/api/v1/notifications/run-escalations", null);
        esc.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Acc25_ComponentBatteryCompatibilityAndBatteryRecords()
    {
        await LoginAs("gestor");
        var orgs = await _client.GetFromJsonAsync<List<OrgUnitDto>>("/api/v1/org-units", JsonOptions);
        var unitA = orgs!.First(o => o.Code == "UTVAR-A");

        var uas = await (await _client.PostAsJsonAsync("/api/v1/uas", new CreateUasRequest(
            "SN-ACC25-001", "DJI", "M300", "MULTI_ROTOR", unitA.Id, null)))
            .Content.ReadFromJsonAsync<UasDto>(JsonOptions);
        Assert.NotNull(uas);
        await _client.PostAsync($"/api/v1/uas/{uas.Id}/submit", null);
        await LoginAs("schvalovatel");
        uas = await (await _client.PostAsync($"/api/v1/uas/{uas.Id}/approve", null)).Content.ReadFromJsonAsync<UasDto>(JsonOptions);
        Assert.NotNull(uas);
        await LoginAs("gestor");

        var component = await (await _client.PostAsJsonAsync("/api/v1/components", new CreateComponentRequest(
            "Gimbal ACC25", null, "GIMBAL", "CSN-ACC25", unitA.Id, "CMP-ACC25")))
            .Content.ReadFromJsonAsync<ComponentDto>(JsonOptions);
        Assert.NotNull(component);

        var blockedRule = await _client.PostAsJsonAsync($"/api/v1/components/{component.Id}/compatibility",
            new CreateCompatibilityRuleRequest(uas.Id, null, CompatibilityStatus.Incompatible,
                DateTimeOffset.UtcNow.AddDays(-1), null, "Nie je schvalene pre tento UAS"));
        blockedRule.EnsureSuccessStatusCode();

        var installBlocked = await _client.PostAsJsonAsync($"/api/v1/components/{component.Id}/installations",
            new InstallComponentRequest(uas.Id, DateTimeOffset.UtcNow, "ACC25"));
        Assert.Equal(HttpStatusCode.BadRequest, installBlocked.StatusCode);

        var okRule = await _client.PostAsJsonAsync($"/api/v1/components/{component.Id}/compatibility",
            new CreateCompatibilityRuleRequest(null, "MULTI_ROTOR", CompatibilityStatus.Compatible,
                DateTimeOffset.UtcNow.AddDays(-1), null, null));
        okRule.EnsureSuccessStatusCode();

        // Incompatible specific UAS still wins over type Compatible
        var stillBlocked = await _client.PostAsJsonAsync($"/api/v1/components/{component.Id}/installations",
            new InstallComponentRequest(uas.Id, DateTimeOffset.UtcNow, "ACC25"));
        Assert.Equal(HttpStatusCode.BadRequest, stillBlocked.StatusCode);

        var otherUas = await (await _client.PostAsJsonAsync("/api/v1/uas", new CreateUasRequest(
            "SN-ACC25-002", "DJI", "Air3", "MULTI_ROTOR", unitA.Id, null)))
            .Content.ReadFromJsonAsync<UasDto>(JsonOptions);
        Assert.NotNull(otherUas);
        await _client.PostAsync($"/api/v1/uas/{otherUas.Id}/submit", null);
        await LoginAs("schvalovatel");
        otherUas = await (await _client.PostAsync($"/api/v1/uas/{otherUas.Id}/approve", null)).Content.ReadFromJsonAsync<UasDto>(JsonOptions);
        Assert.NotNull(otherUas);
        await LoginAs("gestor");

        var installOk = await _client.PostAsJsonAsync($"/api/v1/components/{component.Id}/installations",
            new InstallComponentRequest(otherUas.Id, DateTimeOffset.UtcNow, "ACC25 ok"));
        installOk.EnsureSuccessStatusCode();

        var battery = await (await _client.PostAsJsonAsync("/api/v1/batteries", new CreateBatteryRequest(
            "BAT-ACC25", "BSN-ACC25", "DJI", "TB60", "MULTI_ROTOR", unitA.Id)))
            .Content.ReadFromJsonAsync<BatteryDto>(JsonOptions);
        Assert.NotNull(battery);

        var batCompat = await _client.PostAsJsonAsync($"/api/v1/batteries/{battery.Id}/compatibility",
            new CreateCompatibilityRuleRequest(otherUas.Id, null, CompatibilityStatus.Compatible,
                DateTimeOffset.UtcNow.AddDays(-1), null, null));
        batCompat.EnsureSuccessStatusCode();

        var tech = await _client.PostAsJsonAsync($"/api/v1/batteries/{battery.Id}/technical-records",
            new CreateBatteryTechnicalRecordRequest(DateTimeOffset.UtcNow, BatteryTechnicalMetric.HealthPercent, 92m, "%", null));
        tech.EnsureSuccessStatusCode();

        var opRec = await _client.PostAsJsonAsync($"/api/v1/batteries/{battery.Id}/operation-records",
            new CreateBatteryOperationRecordRequest(BatteryOperationEventType.Inspection, DateTimeOffset.UtcNow,
                88, 12, 25.5m, "OK", "Kontrola ACC25"));
        opRec.EnsureSuccessStatusCode();

        var batteriesBefore = await _client.GetFromJsonAsync<List<BatteryDto>>("/api/v1/batteries", JsonOptions);
        battery = batteriesBefore!.First(b => b.Id == battery.Id);

        battery = await (await _client.PostAsJsonAsync($"/api/v1/batteries/{battery.Id}/transition",
            new TransitionBatteryStatusRequest(AssetUsabilityStatus.Suspended, "Karantena ACC25", battery.RowVersion)))
            .Content.ReadFromJsonAsync<BatteryDto>(JsonOptions);
        Assert.NotNull(battery);
        Assert.Equal(AssetUsabilityStatus.Suspended, battery.Status);

        var history = await _client.GetFromJsonAsync<List<BatteryStatusHistoryDto>>(
            $"/api/v1/batteries/{battery.Id}/status-history", JsonOptions);
        Assert.NotNull(history);
        Assert.Contains(history, h => h.NewStatus == AssetUsabilityStatus.Suspended && h.PreviousStatus == AssetUsabilityStatus.Available);

        var batteries = await _client.GetFromJsonAsync<List<BatteryDto>>("/api/v1/batteries", JsonOptions);
        var updated = batteries!.First(b => b.Id == battery.Id);
        Assert.Equal(92m, updated.HealthPercent);
        Assert.Equal(12, updated.CycleCount);
        Assert.Equal(88m, updated.CapacityPercent);
    }

    [Fact]
    public async Task Acc26_OperationApprovalSteps_Conditions_AndRecurrence()
    {
        await LoginAs("gestor");
        var orgs = await _client.GetFromJsonAsync<List<OrgUnitDto>>("/api/v1/org-units", JsonOptions);
        var pilots = await _client.GetFromJsonAsync<List<PilotDto>>("/api/v1/pilots", JsonOptions);
        var unitA = orgs!.First(o => o.Code == "UTVAR-A");
        var fitPilot = pilots!.First(p => p.LicenseNumber == "PIL-001");

        var uas = await (await _client.PostAsJsonAsync("/api/v1/uas", new CreateUasRequest(
            "SN-ACC26-001", "DJI", "M300", null, unitA.Id, null))).Content.ReadFromJsonAsync<UasDto>(JsonOptions);
        Assert.NotNull(uas);
        await _client.PostAsync($"/api/v1/uas/{uas.Id}/submit", null);
        await LoginAs("schvalovatel");
        uas = await (await _client.PostAsync($"/api/v1/uas/{uas.Id}/approve", null)).Content.ReadFromJsonAsync<UasDto>(JsonOptions);
        Assert.NotNull(uas);

        await LoginAs("gestor");
        var start = DateTimeOffset.UtcNow.AddDays(7);
        var op = await (await _client.PostAsJsonAsync("/api/v1/operations", new CreateOperationRequest(
            "ACC26 trasa", "PATROL", unitA.Id, start, start.AddHours(1),
            null, null, null, null, null, false, null))).Content.ReadFromJsonAsync<OperationDto>(JsonOptions);
        Assert.NotNull(op);

        (await _client.PostAsJsonAsync($"/api/v1/operations/{op.Id}/resources",
            new AssignOperationResourceRequest(OperationResourceType.Uas, uas.Id, null))).EnsureSuccessStatusCode();
        (await _client.PostAsJsonAsync($"/api/v1/operations/{op.Id}/resources",
            new AssignOperationResourceRequest(OperationResourceType.Pilot, fitPilot.Id, "REMOTE_PILOT"))).EnsureSuccessStatusCode();

        (await _client.PostAsJsonAsync($"/api/v1/operations/{op.Id}/approval-steps",
            new AddApprovalStepRequest(1, "UNIT_REVIEW", nameof(SystemRole.UnitAdministrator), null))).EnsureSuccessStatusCode();
        (await _client.PostAsJsonAsync($"/api/v1/operations/{op.Id}/approval-steps",
            new AddApprovalStepRequest(2, "FINAL_APPROVAL", nameof(SystemRole.Approver), null))).EnsureSuccessStatusCode();

        var recurrence = await (await _client.PostAsJsonAsync($"/api/v1/operations/{op.Id}/recurrence",
            new SetRecurrenceRequest(RecurrenceType.Weekly, 1, start, start.AddMonths(2), 4, null)))
            .Content.ReadFromJsonAsync<OperationRecurrenceRuleDto>(JsonOptions);
        Assert.NotNull(recurrence);
        Assert.Equal(RecurrenceType.Weekly, recurrence.RecurrenceType);

        op = await (await _client.PostAsJsonAsync($"/api/v1/operations/{op.Id}/submit", new { rowVersion = op.RowVersion }))
            .Content.ReadFromJsonAsync<OperationDto>(JsonOptions);
        Assert.NotNull(op);
        Assert.Equal(OperationStatus.Submitted, op.Status);

        await LoginAs("schvalovatel");
        var afterStep1 = await (await _client.PostAsJsonAsync($"/api/v1/operations/{op.Id}/approve",
            new DecisionRequest("1. krok OK", op.RowVersion)))
            .Content.ReadFromJsonAsync<OperationDto>(JsonOptions);
        Assert.NotNull(afterStep1);
        Assert.Equal(OperationStatus.Submitted, afterStep1.Status);

        var detailMid = await _client.GetFromJsonAsync<OperationDetailDto>($"/api/v1/operations/{op.Id}", JsonOptions);
        Assert.NotNull(detailMid);
        Assert.Contains(detailMid.ApprovalSteps, s => s.SequenceNumber == 1 && s.Status == ApprovalStepStatus.Approved);
        Assert.Contains(detailMid.ApprovalSteps, s => s.SequenceNumber == 2 && s.Status == ApprovalStepStatus.Pending);

        var final = await (await _client.PostAsJsonAsync($"/api/v1/operations/{op.Id}/approve",
            new DecisionRequest("Podmienene schvalene", afterStep1.RowVersion,
                OperationDecisionCode.ConditionallyApproved,
                [new CreateApprovalConditionRequest("WEATHER", "Overit pocasie pred letom", true)])))
            .Content.ReadFromJsonAsync<OperationDto>(JsonOptions);
        Assert.NotNull(final);
        Assert.Equal(OperationStatus.Approved, final.Status);

        var detail = await _client.GetFromJsonAsync<OperationDetailDto>($"/api/v1/operations/{op.Id}", JsonOptions);
        Assert.NotNull(detail);
        Assert.NotNull(detail.Recurrence);
        Assert.NotEmpty(detail.Conditions);
        var condition = detail.Conditions.First();
        Assert.Equal(ApprovalConditionStatus.Unmet, condition.Status);

        await LoginAs("gestor");
        var flightBlocked = await _client.PostAsJsonAsync("/api/v1/flights/from-operation",
            new CreateFlightFromOperationRequest(op.Id, null, null, null, null, null));
        Assert.Equal(HttpStatusCode.BadRequest, flightBlocked.StatusCode);

        var verified = await (await _client.PostAsJsonAsync($"/api/v1/approval-conditions/{condition.Id}/verify",
            new VerifyApprovalConditionRequest("Pocasie OK", condition.RowVersion)))
            .Content.ReadFromJsonAsync<OperationApprovalConditionDto>(JsonOptions);
        Assert.NotNull(verified);
        Assert.Equal(ApprovalConditionStatus.Met, verified.Status);

        var generated = await (await _client.PostAsJsonAsync($"/api/v1/operations/{op.Id}/recurrence/generate?count=2", new { }))
            .Content.ReadFromJsonAsync<List<OperationDto>>(JsonOptions);
        Assert.NotNull(generated);
        Assert.Equal(2, generated.Count);
        Assert.All(generated, g => Assert.Equal(OperationStatus.Draft, g.Status));
    }

    [Fact]
    public async Task Acc27_ServiceRequest_Restriction_AndReleaseToService()
    {
        await LoginAs("gestor");
        var orgs = await _client.GetFromJsonAsync<List<OrgUnitDto>>("/api/v1/org-units", JsonOptions);
        var unitA = orgs!.First(o => o.Code == "UTVAR-A");

        var uas = await (await _client.PostAsJsonAsync("/api/v1/uas", new CreateUasRequest(
            "SN-ACC27-001", "DJI", "M300", null, unitA.Id, null))).Content.ReadFromJsonAsync<UasDto>(JsonOptions);
        Assert.NotNull(uas);
        await _client.PostAsync($"/api/v1/uas/{uas.Id}/submit", null);
        await LoginAs("schvalovatel");
        uas = await (await _client.PostAsync($"/api/v1/uas/{uas.Id}/approve", null)).Content.ReadFromJsonAsync<UasDto>(JsonOptions);
        Assert.NotNull(uas);
        await LoginAs("gestor");
        await _client.PostAsJsonAsync($"/api/v1/uas/{uas.Id}/transition",
            new TransitionUasRequest(UasLifecycleStatus.InService, "Do prevadzky", uas.RowVersion));
        uas = (await _client.GetFromJsonAsync<UasDetailDto>($"/api/v1/uas/{uas.Id}", JsonOptions))!.Uas;

        var defect = await (await _client.PostAsJsonAsync("/api/v1/defects", new ReportDefectRequest(
            MaintenanceTargetType.Uas, uas.Id, "ACC27 porucha", "Motor vibruje",
            DefectSeverity.Blocking, true, null, null, uas.Id, "MOTOR")))
            .Content.ReadFromJsonAsync<DefectDto>(JsonOptions);
        Assert.NotNull(defect);

        var sr = await (await _client.PostAsJsonAsync("/api/v1/service-requests", new CreateServiceRequestRequest(
            "REPAIR", "DEFECT", defect.Id, MaintenanceTargetType.Uas, uas.Id,
            "Oprava motora ACC27", ServiceRequestPriority.High, null, null)))
            .Content.ReadFromJsonAsync<ServiceRequestDto>(JsonOptions);
        Assert.NotNull(sr);
        Assert.Equal(ServiceRequestStatus.New, sr.Status);

        var wo = await (await _client.PostAsJsonAsync("/api/v1/work-orders", new CreateWorkOrderRequest(
            null, MaintenanceTargetType.Uas, uas.Id, "WO ACC27", "Oprava", null, null, sr.Id)))
            .Content.ReadFromJsonAsync<WorkOrderDto>(JsonOptions);
        Assert.NotNull(wo);
        Assert.Equal(sr.Id, wo.ServiceRequestId);

        (await _client.PostAsJsonAsync($"/api/v1/work-orders/{wo.Id}/complete", new CompleteWorkOrderRequest(
            "Vymena loziska", "OK", DateTimeOffset.UtcNow, "lozisko", null, false, wo.RowVersion)))
            .EnsureSuccessStatusCode();

        // Dokončenie WO samo o sebe neuvoľní — blokuje stále porucha
        var blockAfterWo = await _client.GetFromJsonAsync<AssetBlockCheckDto>($"/api/v1/uas/{uas.Id}/block-status", JsonOptions);
        Assert.NotNull(blockAfterWo);
        Assert.True(blockAfterWo.IsBlocked);

        var restriction = await (await _client.PostAsJsonAsync("/api/v1/technical-restrictions",
            new CreateTechnicalRestrictionRequest(defect.Id, MaintenanceTargetType.Uas, uas.Id,
                "FLIGHT_LIMIT", "Len denné lety", DateTimeOffset.UtcNow.AddMinutes(-1), null, true)))
            .Content.ReadFromJsonAsync<TechnicalRestrictionDto>(JsonOptions);
        Assert.NotNull(restriction);
        Assert.Equal(TechnicalRestrictionStatus.Active, restriction.Status);

        var release = await (await _client.PostAsJsonAsync("/api/v1/releases-to-service",
            new CreateReleaseToServiceRequest(wo.Id, defect.Id, MaintenanceTargetType.Uas, uas.Id,
                ReleaseToServiceDecision.Released, "FULL", null, "Uvolnene po oprave ACC27", null)))
            .Content.ReadFromJsonAsync<ReleaseToServiceDto>(JsonOptions);
        Assert.NotNull(release);
        Assert.Equal(ReleaseToServiceDecision.Released, release.Decision);

        var block = await _client.GetFromJsonAsync<AssetBlockCheckDto>($"/api/v1/uas/{uas.Id}/block-status", JsonOptions);
        Assert.NotNull(block);
        Assert.False(block.IsBlocked);

        var history = await _client.GetFromJsonAsync<List<TechnicalStatusHistoryDto>>(
            $"/api/v1/technical-status-history?targetType=Uas&targetId={uas.Id}", JsonOptions);
        Assert.NotNull(history);
        Assert.Contains(history, h => h.NewStatusCode == "RELEASED");

        var srs = await _client.GetFromJsonAsync<List<ServiceRequestDto>>("/api/v1/service-requests", JsonOptions);
        Assert.Contains(srs!, s => s.Id == sr.Id && s.Status == ServiceRequestStatus.Realized);
    }

    [Fact]
    public async Task Acc28_PilotFunctions_Restrictions_Verification_AndTrainingCatalog()
    {
        await LoginAs("gestor");
        var orgs = await _client.GetFromJsonAsync<List<OrgUnitDto>>("/api/v1/org-units", JsonOptions);
        Assert.NotNull(orgs);
        var unitA = orgs.First(o => o.Code == "UTVAR-A");

        var pilots = await _client.GetFromJsonAsync<List<PilotDto>>("/api/v1/pilots", JsonOptions);
        Assert.NotNull(pilots);
        var fit = pilots.First(p => p.LicenseNumber == "PIL-001");

        var fnResp = await _client.PostAsJsonAsync($"/api/v1/pilots/{fit.Id}/functions",
            new CreatePersonFunctionAssignmentRequest(
                "PILOT", unitA.Id, unitA.Id, "ORG_UNIT", null,
                DateTimeOffset.UtcNow.AddDays(-1), null));
        fnResp.EnsureSuccessStatusCode();
        var fn = await fnResp.Content.ReadFromJsonAsync<PersonFunctionAssignmentDto>(JsonOptions);
        Assert.NotNull(fn);
        Assert.Equal("PILOT", fn.FunctionTypeCode);

        var detail = await _client.GetFromJsonAsync<PilotDetailDto>($"/api/v1/pilots/{fit.Id}", JsonOptions);
        Assert.NotNull(detail);
        Assert.Contains(detail.Functions, f => f.Id == fn.Id);

        var cert = detail.Certificates.FirstOrDefault(c => c.Status == CertificateStatus.Issued)
            ?? throw new InvalidOperationException("Seeded fit pilot must have issued certificate.");
        var scopeResp = await _client.PostAsJsonAsync($"/api/v1/pilots/{fit.Id}/certificates/{cert.Id}/scopes",
            new CreateCertificateScopeRequest(
                "UAS_TYPE", "MULTI_ROTOR", "VLOS", "PILOT",
                DateTimeOffset.UtcNow.AddDays(-1), null, null));
        scopeResp.EnsureSuccessStatusCode();
        var scope = await scopeResp.Content.ReadFromJsonAsync<CertificateScopeDto>(JsonOptions);
        Assert.NotNull(scope);
        Assert.Equal("MULTI_ROTOR", scope.UasTypeCode);

        var qualResp = await _client.PostAsJsonAsync($"/api/v1/pilots/{fit.Id}/qualifications",
            new CreatePilotQualificationRequest(
                "A2", "Doklad A2 ACC28", DateTimeOffset.UtcNow.AddYears(-1),
                DateTimeOffset.UtcNow.AddYears(1), "Transportný úrad"));
        qualResp.EnsureSuccessStatusCode();
        var qual = await qualResp.Content.ReadFromJsonAsync<PilotQualificationDto>(JsonOptions);
        Assert.NotNull(qual);

        var verificationResp = await _client.PostAsJsonAsync(
            $"/api/v1/pilots/{fit.Id}/qualifications/{qual.Id}/verifications",
            new CreateQualificationVerificationRequest(
                "REGISTER_CHECK", QualificationVerificationResult.Verified,
                "Centrálny register", DateTimeOffset.UtcNow.AddYears(1), null));
        verificationResp.EnsureSuccessStatusCode();
        var verification = await verificationResp.Content.ReadFromJsonAsync<QualificationVerificationDto>(JsonOptions);
        Assert.NotNull(verification);
        Assert.Equal(QualificationVerificationResult.Verified, verification.Result);

        var catalogResp = await _client.PostAsJsonAsync("/api/v1/training-catalog",
            new CreateTrainingCatalogItemRequest(
                "CURR-ACC28", "Kondičný výcvik ACC28", "CURRENCY",
                "Opakovací výcvik pre Acc28", 4, 12, true, 70, true,
                DateTimeOffset.UtcNow.AddDays(-1), null));
        catalogResp.EnsureSuccessStatusCode();
        var catalog = await catalogResp.Content.ReadFromJsonAsync<TrainingCatalogItemDto>(JsonOptions);
        Assert.NotNull(catalog);

        var activityResp = await _client.PostAsJsonAsync("/api/v1/training-activities",
            new CreateTrainingActivityRequest(
                catalog.Id, "TA-ACC28-001", unitA.Id, unitA.Id,
                DateTimeOffset.UtcNow.AddDays(1), DateTimeOffset.UtcNow.AddDays(2),
                "Hangár A", "IN_PERSON", null, null, 10));
        activityResp.EnsureSuccessStatusCode();
        var activity = await activityResp.Content.ReadFromJsonAsync<TrainingActivityDto>(JsonOptions);
        Assert.NotNull(activity);

        var participationResp = await _client.PostAsJsonAsync(
            $"/api/v1/training-activities/{activity.Id}/participations",
            new CreateTrainingParticipationRequest(fit.PersonId, "PARTICIPANT"));
        participationResp.EnsureSuccessStatusCode();
        var participation = await participationResp.Content.ReadFromJsonAsync<TrainingParticipationDto>(JsonOptions);
        Assert.NotNull(participation);

        var resultResp = await _client.PostAsJsonAsync(
            $"/api/v1/training-activities/{activity.Id}/participations/{participation.Id}/result",
            new RecordTrainingActivityResultRequest(
                TrainingResult.Passed, 1, 80, 90, 85, null,
                DateTimeOffset.UtcNow.AddMonths(12), "Úspešne ACC28", true));
        resultResp.EnsureSuccessStatusCode();
        var result = await resultResp.Content.ReadFromJsonAsync<TrainingActivityResultDto>(JsonOptions);
        Assert.NotNull(result);
        Assert.Equal(TrainingResult.Passed, result.Result);

        detail = await _client.GetFromJsonAsync<PilotDetailDto>($"/api/v1/pilots/{fit.Id}", JsonOptions);
        Assert.NotNull(detail);
        Assert.Contains(detail.Trainings, t => t.TrainingParticipationId == participation.Id);

        var eligibleBefore = await _client.GetFromJsonAsync<PilotEligibilityCheckDto>(
            $"/api/v1/pilots/{fit.Id}/eligibility", JsonOptions);
        Assert.NotNull(eligibleBefore);
        Assert.True(eligibleBefore.IsEligible);

        var restrictionResp = await _client.PostAsJsonAsync($"/api/v1/pilots/{fit.Id}/restrictions",
            new CreatePilotRestrictionRequest(
                "MEDICAL", "Dočasné zdravotné obmedzenie ACC28", "HIGH", true,
                DateTimeOffset.UtcNow.AddMinutes(-1), null, null));
        restrictionResp.EnsureSuccessStatusCode();
        var restriction = await restrictionResp.Content.ReadFromJsonAsync<PilotRestrictionDto>(JsonOptions);
        Assert.NotNull(restriction);
        Assert.True(restriction.BlocksOperation);

        var blocked = await _client.GetFromJsonAsync<PilotEligibilityCheckDto>(
            $"/api/v1/pilots/{fit.Id}/eligibility", JsonOptions);
        Assert.NotNull(blocked);
        Assert.False(blocked.IsEligible);
        Assert.Contains("obmedzenie", blocked.Reason, StringComparison.OrdinalIgnoreCase);

        (await _client.PostAsJsonAsync($"/api/v1/pilots/{fit.Id}/restrictions/{restriction.Id}/end",
            new EndPilotRestrictionRequest("Ukončené po kontrole ACC28")))
            .EnsureSuccessStatusCode();

        var eligibleAfter = await _client.GetFromJsonAsync<PilotEligibilityCheckDto>(
            $"/api/v1/pilots/{fit.Id}/eligibility", JsonOptions);
        Assert.NotNull(eligibleAfter);
        Assert.True(eligibleAfter.IsEligible);
    }

    [Fact]
    public async Task Acc29_SafetyParticipants_Causes_Reporting_AndFleetAlert()
    {
        await LoginAs("gestor");
        var orgs = await _client.GetFromJsonAsync<List<OrgUnitDto>>("/api/v1/org-units", JsonOptions);
        Assert.NotNull(orgs);
        var unitA = orgs.First(o => o.Code == "UTVAR-A");

        var pilots = await _client.GetFromJsonAsync<List<PilotDto>>("/api/v1/pilots", JsonOptions);
        Assert.NotNull(pilots);
        var pilot = pilots.First(p => p.LicenseNumber == "PIL-001");

        var uasCreate = await _client.PostAsJsonAsync("/api/v1/uas", new CreateUasRequest(
            "SN-ACC29-001", "DJI", "Mavic", null, unitA.Id, null));
        uasCreate.EnsureSuccessStatusCode();
        var uas = await uasCreate.Content.ReadFromJsonAsync<UasDto>(JsonOptions);
        Assert.NotNull(uas);

        var createResp = await _client.PostAsJsonAsync("/api/v1/safety-events", new CreateSafetyEventRequest(
            "ACC29 nehoda pri pristati", "Minimalne hlasenie ACC29", SafetySeverity.High,
            DateTimeOffset.UtcNow, unitA.Id, null, null, uas.Id, "Letisko ACC29"));
        createResp.EnsureSuccessStatusCode();
        var safety = await createResp.Content.ReadFromJsonAsync<SafetyEventDto>(JsonOptions);
        Assert.NotNull(safety);

        var submitResp = await _client.PostAsJsonAsync($"/api/v1/safety-events/{safety.Id}/submit",
            new SubmitSafetyEventRequest(safety.RowVersion));
        submitResp.EnsureSuccessStatusCode();
        safety = await submitResp.Content.ReadFromJsonAsync<SafetyEventDto>(JsonOptions);
        Assert.NotNull(safety);

        var participantResp = await _client.PostAsJsonAsync($"/api/v1/safety-events/{safety.Id}/participants",
            new CreateSafetyEventParticipantRequest(
                pilot.PersonId, "PILOT", "Pilot v čase udalosti", null, true, DateTimeOffset.UtcNow, null));
        participantResp.EnsureSuccessStatusCode();
        var participant = await participantResp.Content.ReadFromJsonAsync<SafetyEventParticipantDto>(JsonOptions);
        Assert.NotNull(participant);

        var assetResp = await _client.PostAsJsonAsync($"/api/v1/safety-events/{safety.Id}/assets",
            new CreateSafetyEventAssetRequest(
                uas.Id, null, null, "PRIMARY", "DAMAGED", "Poškodený podvozok", "GROUNDED", true, null));
        assetResp.EnsureSuccessStatusCode();

        var immediateResp = await _client.PostAsJsonAsync($"/api/v1/safety-events/{safety.Id}/immediate-actions",
            new CreateSafetyImmediateActionRequest(
                "SECURE_AREA", "Zabezpečenie miesta pristátia", DateTimeOffset.UtcNow,
                pilot.PersonId, "Priestor zabezpečený", true));
        immediateResp.EnsureSuccessStatusCode();

        var invResp = await _client.PostAsJsonAsync($"/api/v1/safety-events/{safety.Id}/investigations",
            new CreateSafetyInvestigationRequest(
                "INV-ACC29-001", "INTERNAL", pilot.PersonId,
                "Preverenie pristávacieho manévru ACC29", DateTimeOffset.UtcNow.AddDays(14)));
        invResp.EnsureSuccessStatusCode();
        var investigation = await invResp.Content.ReadFromJsonAsync<SafetyInvestigationDto>(JsonOptions);
        Assert.NotNull(investigation);

        var causeResp = await _client.PostAsJsonAsync(
            $"/api/v1/safety-events/{safety.Id}/investigations/{investigation.Id}/causes",
            new CreateSafetyCauseRequest(
                "HUMAN", "Nesprávny odhad výšky", true, SafetyCauseStatus.Confirmed, "HIGH", "Svedectvo + foto"));
        causeResp.EnsureSuccessStatusCode();
        var cause = await causeResp.Content.ReadFromJsonAsync<SafetyCauseDto>(JsonOptions);
        Assert.NotNull(cause);
        Assert.True(cause.IsRootCause);

        var obligationResp = await _client.PostAsJsonAsync($"/api/v1/safety-events/{safety.Id}/reporting-obligations",
            new CreateSafetyReportingObligationRequest(
                "EXTERNAL", "Dopravný úrad SR", "Interný predpis BP-18",
                DateTimeOffset.UtcNow.AddDays(3)));
        obligationResp.EnsureSuccessStatusCode();
        var obligation = await obligationResp.Content.ReadFromJsonAsync<SafetyReportingObligationDto>(JsonOptions);
        Assert.NotNull(obligation);
        Assert.Equal(SafetyReportingObligationStatus.Pending, obligation.Status);

        safety = (await _client.GetFromJsonAsync<SafetyEventDetailDto>($"/api/v1/safety-events/{safety.Id}", JsonOptions))!.Event;
        var closeBlocked = await _client.PostAsJsonAsync($"/api/v1/safety-events/{safety.Id}/close",
            new CloseSafetyEventRequest("Predcasne ACC29", null, safety.RowVersion));
        Assert.Equal(HttpStatusCode.BadRequest, closeBlocked.StatusCode);

        var fulfillResp = await _client.PostAsJsonAsync(
            $"/api/v1/safety-events/{safety.Id}/reporting-obligations/{obligation.Id}/fulfill",
            new FulfillSafetyReportingObligationRequest(pilot.PersonId, "DU-ACC29-42", null));
        fulfillResp.EnsureSuccessStatusCode();
        obligation = await fulfillResp.Content.ReadFromJsonAsync<SafetyReportingObligationDto>(JsonOptions);
        Assert.NotNull(obligation);
        Assert.Equal(SafetyReportingObligationStatus.Fulfilled, obligation.Status);

        var alertResp = await _client.PostAsJsonAsync($"/api/v1/safety-events/{safety.Id}/fleet-alerts",
            new CreateFleetSafetyAlertRequest(
                "FSA-ACC29-001", "Pozor na pristátie Mavic",
                "Skontrolujte výcvik pristávacích manévrov pre typ Mavic.",
                "HIGH", "UAS_TYPE", "MULTI_ROTOR", true));
        alertResp.EnsureSuccessStatusCode();
        var alert = await alertResp.Content.ReadFromJsonAsync<FleetSafetyAlertDto>(JsonOptions);
        Assert.NotNull(alert);
        Assert.Equal(FleetSafetyAlertStatus.Published, alert.Status);

        var notifications = await _client.GetFromJsonAsync<List<AppNotificationDto>>("/api/v1/notifications", JsonOptions);
        Assert.NotNull(notifications);
        Assert.Contains(notifications, n => n.TypeCode == "FLEET_SAFETY_ALERT" && n.RelatedSafetyEventId == safety.Id);

        var severityResp = await _client.PostAsJsonAsync($"/api/v1/safety-events/{safety.Id}/severity",
            new ChangeSafetySeverityRequest(SafetySeverity.Medium, "Preklasifikované po preverení ACC29", safety.RowVersion));
        severityResp.EnsureSuccessStatusCode();
        safety = await severityResp.Content.ReadFromJsonAsync<SafetyEventDto>(JsonOptions);
        Assert.NotNull(safety);
        Assert.Equal(SafetySeverity.Medium, safety.Severity);

        var detail = await _client.GetFromJsonAsync<SafetyEventDetailDto>($"/api/v1/safety-events/{safety.Id}", JsonOptions);
        Assert.NotNull(detail);
        Assert.Contains(detail.Participants, p => p.Id == participant.Id);
        Assert.Contains(detail.Assets, a => a.UasId == uas.Id);
        Assert.NotEmpty(detail.ImmediateActions);
        Assert.Contains(detail.Investigations, i => i.Id == investigation.Id && i.Causes.Any(c => c.Id == cause.Id));
        Assert.Contains(detail.ReportingObligations, o => o.Id == obligation.Id && o.Status == SafetyReportingObligationStatus.Fulfilled);
        Assert.Contains(detail.FleetAlerts, a => a.Id == alert.Id);
        Assert.Contains(detail.StatusHistory, h => h.NewStatus == SafetyEventStatus.Submitted);
        Assert.Contains(detail.ClassificationHistory, h => h.NewValueCode == nameof(SafetySeverity.Medium));

        var closeResp = await _client.PostAsJsonAsync($"/api/v1/safety-events/{safety.Id}/close",
            new CloseSafetyEventRequest("ACC29 uzavreté po splnení povinností", null, safety.RowVersion));
        closeResp.EnsureSuccessStatusCode();
        safety = await closeResp.Content.ReadFromJsonAsync<SafetyEventDto>(JsonOptions);
        Assert.NotNull(safety);
        Assert.Equal(SafetyEventStatus.Closed, safety.Status);
    }

    [Fact]
    public async Task Acc30_PhaseF_MapCheck_EmailDelivery_AndIntegrationProviders()
    {
        await LoginAs("gestor");

        var info = await _client.GetFromJsonAsync<SystemInfoDto>("/api/v1/system/info", JsonOptions);
        Assert.NotNull(info);
        Assert.Equal("Local", info.StorageProvider);
        Assert.Equal("Null", info.EmailProvider);
        Assert.Equal("Offline", info.AirspaceProvider);
        Assert.False(info.OidcConfigured);

        var sso = await _client.GetFromJsonAsync<SsoStartResponse>("/api/v1/auth/sso/start?returnUrl=/dashboard", JsonOptions);
        Assert.NotNull(sso);
        Assert.Equal("dev", sso.Status);
        Assert.NotNull(sso.RedirectUrl);
        Assert.Contains("dev-sso%3Agestor", sso.RedirectUrl);

        var orgs = await _client.GetFromJsonAsync<List<OrgUnitDto>>("/api/v1/org-units", JsonOptions);
        Assert.NotNull(orgs);
        var unitA = orgs.First(o => o.Code == "UTVAR-A");
        var pilots = await _client.GetFromJsonAsync<List<PilotDto>>("/api/v1/pilots", JsonOptions);
        var fitPilot = pilots!.First(p => p.LicenseNumber == "PIL-001");

        var uas = await (await _client.PostAsJsonAsync("/api/v1/uas", new CreateUasRequest(
            "SN-ACC30-001", "DJI", "Air3", null, unitA.Id, null))).Content.ReadFromJsonAsync<UasDto>(JsonOptions);
        Assert.NotNull(uas);
        await _client.PostAsync($"/api/v1/uas/{uas.Id}/submit", null);
        await LoginAs("schvalovatel");
        uas = await (await _client.PostAsync($"/api/v1/uas/{uas.Id}/approve", null)).Content.ReadFromJsonAsync<UasDto>(JsonOptions);
        Assert.NotNull(uas);
        await LoginAs("gestor");

        var clearOpResp = await _client.PostAsJsonAsync("/api/v1/operations", new CreateOperationRequest(
            "ACC30 clear map", "TRAINING", unitA.Id,
            DateTimeOffset.UtcNow.AddHours(2), DateTimeOffset.UtcNow.AddHours(3),
            "Žilina", 49.22, 18.74,
            """{"type":"Point","coordinates":[18.74,49.22]}""",
            "Map check clear", false, null));
        clearOpResp.EnsureSuccessStatusCode();
        var clearOp = await clearOpResp.Content.ReadFromJsonAsync<OperationDto>(JsonOptions);
        Assert.NotNull(clearOp);
        await _client.PostAsJsonAsync($"/api/v1/operations/{clearOp.Id}/resources",
            new AssignOperationResourceRequest(OperationResourceType.Uas, uas.Id, "PRIMARY"));
        await _client.PostAsJsonAsync($"/api/v1/operations/{clearOp.Id}/resources",
            new AssignOperationResourceRequest(OperationResourceType.Pilot, fitPilot.Id, "PIC"));

        var clearCheckResp = await _client.PostAsync($"/api/v1/operations/{clearOp.Id}/map-check", null);
        clearCheckResp.EnsureSuccessStatusCode();
        var clearCheck = await clearCheckResp.Content.ReadFromJsonAsync<OperationMapCheckDto>(JsonOptions);
        Assert.NotNull(clearCheck);
        Assert.Equal("CLEAR", clearCheck.ResultCode);
        Assert.False(clearCheck.IsBlocking);
        Assert.Equal("Offline", clearCheck.ProviderCode);

        var conflictOpResp = await _client.PostAsJsonAsync("/api/v1/operations", new CreateOperationRequest(
            "ACC30 restricted map", "TRAINING", unitA.Id,
            DateTimeOffset.UtcNow.AddHours(4), DateTimeOffset.UtcNow.AddHours(5),
            "Bratislava center", 48.15, 17.11,
            """{"type":"Polygon","properties":{"zone":"RESTRICTED"},"coordinates":[[[17.10,48.14],[17.12,48.14],[17.12,48.16],[17.10,48.16],[17.10,48.14]]]}""",
            "Map check conflict", false, null));
        conflictOpResp.EnsureSuccessStatusCode();
        var conflictOp = await conflictOpResp.Content.ReadFromJsonAsync<OperationDto>(JsonOptions);
        Assert.NotNull(conflictOp);

        var conflictCheckResp = await _client.PostAsync($"/api/v1/operations/{conflictOp.Id}/map-check", null);
        conflictCheckResp.EnsureSuccessStatusCode();
        var conflictCheck = await conflictCheckResp.Content.ReadFromJsonAsync<OperationMapCheckDto>(JsonOptions);
        Assert.NotNull(conflictCheck);
        Assert.Equal("CONFLICT", conflictCheck.ResultCode);
        Assert.True(conflictCheck.IsBlocking);

        var detail = await _client.GetFromJsonAsync<OperationDetailDto>($"/api/v1/operations/{conflictOp.Id}", JsonOptions);
        Assert.NotNull(detail);
        Assert.Contains(detail.MapChecks, m => m.Id == conflictCheck.Id);

        var accounts = await _client.GetFromJsonAsync<List<AccountDto>>("/api/v1/accounts", JsonOptions);
        var gestor = accounts!.First(a => a.UserName == "gestor");
        var emailPublish = await _client.PostAsJsonAsync("/api/v1/notifications", new PublishNotificationRequest(
            null,
            "ACC30 e-mail",
            "Test e-mail doručenia cez Null provider",
            "ACC30_EMAIL",
            "Normal",
            gestor.Id,
            null,
            null,
            "Test",
            null,
            NotificationChannel.Email));
        emailPublish.EnsureSuccessStatusCode();
        var emailNote = await emailPublish.Content.ReadFromJsonAsync<NotificationDetailDto>(JsonOptions);
        Assert.NotNull(emailNote);
        Assert.Contains(emailNote.DeliveryAttempts,
            d => d.Channel == NotificationChannel.Email && d.Status == DeliveryAttemptStatus.Sent);

        var connectors = await _client.GetFromJsonAsync<List<IntegrationConnectorDto>>("/api/v1/integrations", JsonOptions);
        Assert.NotNull(connectors);
        Assert.Contains(connectors, c => c.Code == "LPS_SR");
        Assert.Contains(connectors, c => c.Code == "MINIO_STORAGE");
        Assert.Contains(connectors, c => c.Code == "EMAIL_SMTP");
    }

    [Fact]
    public async Task Acc31_Delegation_GrantsThenRevokesPermission()
    {
        await LoginAs("schvalovatel");
        var denied = await _client.GetAsync("/api/v1/integrations");
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);

        await LoginAs("gestor");
        var accounts = await _client.GetFromJsonAsync<List<AccountDto>>("/api/v1/accounts", JsonOptions);
        var recipient = accounts!.First(a => a.UserName == "schvalovatel");

        var createResp = await _client.PostAsJsonAsync("/api/v1/delegations", new CreateDelegatedPermissionRequest(
            recipient.Id,
            "integrations.read",
            null,
            DateTimeOffset.UtcNow.AddMinutes(-1),
            DateTimeOffset.UtcNow.AddDays(1),
            "Acc31 dočasné delegovanie čítania integrácií"));
        createResp.EnsureSuccessStatusCode();
        var delegation = await createResp.Content.ReadFromJsonAsync<DelegatedPermissionDto>(JsonOptions);
        Assert.NotNull(delegation);
        Assert.True(delegation.IsActive);
        Assert.Equal("integrations.read", delegation.PermissionCode);

        var listed = await _client.GetFromJsonAsync<List<DelegatedPermissionDto>>("/api/v1/delegations", JsonOptions);
        Assert.Contains(listed!, d => d.Id == delegation.Id);

        await LoginAs("schvalovatel");
        var allowed = await _client.GetAsync("/api/v1/integrations");
        allowed.EnsureSuccessStatusCode();
        var connectors = await allowed.Content.ReadFromJsonAsync<List<IntegrationConnectorDto>>(JsonOptions);
        Assert.NotNull(connectors);
        Assert.NotEmpty(connectors);

        await LoginAs("gestor");
        var revokeResp = await _client.PostAsJsonAsync($"/api/v1/delegations/{delegation.Id}/revoke",
            new RevokeDelegatedPermissionRequest("Acc31 revoke", delegation.RowVersion));
        revokeResp.EnsureSuccessStatusCode();
        var revoked = await revokeResp.Content.ReadFromJsonAsync<DelegatedPermissionDto>(JsonOptions);
        Assert.NotNull(revoked);
        Assert.False(revoked.IsActive);

        await LoginAs("schvalovatel");
        var deniedAgain = await _client.GetAsync("/api/v1/integrations");
        Assert.Equal(HttpStatusCode.Forbidden, deniedAgain.StatusCode);
    }

    [Fact]
    public async Task Acc32_Dashboard_OrgUnitFilter_ScopesFleet()
    {
        await LoginAs("gestor");

        var full = await _client.GetFromJsonAsync<DashboardDto>("/api/v1/dashboard", JsonOptions);
        Assert.NotNull(full);

        var orgs = await _client.GetFromJsonAsync<List<OrgUnitDto>>("/api/v1/org-units", JsonOptions);
        Assert.NotNull(orgs);
        var unitA = orgs.First(o => o.Code == "UTVAR-A");
        var unitB = orgs.First(o => o.Code == "UTVAR-B");

        var filteredA = await _client.GetFromJsonAsync<DashboardDto>(
            $"/api/v1/dashboard?orgUnitId={unitA.Id}", JsonOptions);
        Assert.NotNull(filteredA);
        Assert.True(filteredA.Fleet.TotalUas <= full.Fleet.TotalUas);

        var beforeA = filteredA.Fleet.TotalUas;
        var createA = await _client.PostAsJsonAsync("/api/v1/uas", new CreateUasRequest(
            "SN-ACC32-A", "DJI", "Mini3", null, unitA.Id, null));
        createA.EnsureSuccessStatusCode();

        var afterA = await _client.GetFromJsonAsync<DashboardDto>(
            $"/api/v1/dashboard?orgUnitId={unitA.Id}", JsonOptions);
        Assert.NotNull(afterA);
        Assert.Equal(beforeA + 1, afterA.Fleet.TotalUas);

        var filteredB = await _client.GetFromJsonAsync<DashboardDto>(
            $"/api/v1/dashboard?orgUnitId={unitB.Id}", JsonOptions);
        Assert.NotNull(filteredB);
        Assert.Equal(filteredB.Fleet.TotalUas, (await _client.GetFromJsonAsync<DashboardDto>(
            $"/api/v1/dashboard?orgUnitId={unitB.Id}", JsonOptions))!.Fleet.TotalUas);

        var createB = await _client.PostAsJsonAsync("/api/v1/uas", new CreateUasRequest(
            "SN-ACC32-B", "DJI", "Mini3", null, unitB.Id, null));
        createB.EnsureSuccessStatusCode();

        var afterB = await _client.GetFromJsonAsync<DashboardDto>(
            $"/api/v1/dashboard?orgUnitId={unitB.Id}", JsonOptions);
        Assert.NotNull(afterB);
        Assert.Equal(filteredB.Fleet.TotalUas + 1, afterB.Fleet.TotalUas);

        var stillA = await _client.GetFromJsonAsync<DashboardDto>(
            $"/api/v1/dashboard?orgUnitId={unitA.Id}", JsonOptions);
        Assert.NotNull(stillA);
        Assert.Equal(afterA.Fleet.TotalUas, stillA.Fleet.TotalUas);
    }

    [Fact]
    public async Task Acc33_ControlAudit_UiHappyPath_PlannedToClose()
    {
        await LoginAs("gestor");
        var orgs = await _client.GetFromJsonAsync<List<OrgUnitDto>>("/api/v1/org-units", JsonOptions);
        var unitA = orgs!.First(o => o.Code == "UTVAR-A");

        var create = await _client.PostAsJsonAsync("/api/v1/control-audits", new CreateControlAuditRequest(
            "ACC33 UI detail cesta",
            ControlAuditType.InternalAudit,
            unitA.Id,
            null,
            null,
            "Wave 13 UI happy-path",
            [
                new CreateChecklistItemRequest("DOC", "Dokumentácia kompletná?", 1),
                new CreateChecklistItemRequest("PROC", "Proces dodržaný?", 2)
            ]));
        create.EnsureSuccessStatusCode();
        var audit = await create.Content.ReadFromJsonAsync<ControlAuditDto>(JsonOptions);
        Assert.NotNull(audit);
        Assert.Equal(ControlAuditStatus.Planned, audit.Status);

        var plannedDetail = await _client.GetFromJsonAsync<ControlAuditDetailDto>(
            $"/api/v1/control-audits/{audit.Id}", JsonOptions);
        Assert.NotNull(plannedDetail);
        Assert.Equal(2, plannedDetail.Checklist.Count);
        Assert.Empty(plannedDetail.Findings);
        Assert.Empty(plannedDetail.Reports);

        var started = await (await _client.PostAsJsonAsync($"/api/v1/control-audits/{audit.Id}/start",
            new StartControlAuditRequest(audit.RowVersion))).Content.ReadFromJsonAsync<ControlAuditDto>(JsonOptions);
        Assert.NotNull(started);
        Assert.Equal(ControlAuditStatus.InProgress, started.Status);

        var detail = await _client.GetFromJsonAsync<ControlAuditDetailDto>($"/api/v1/control-audits/{audit.Id}", JsonOptions);
        Assert.NotNull(detail);
        foreach (var item in detail.Checklist)
        {
            var res = await _client.PostAsJsonAsync(
                $"/api/v1/control-audits/{audit.Id}/checklist/{item.Id}",
                new UpdateChecklistResultRequest(
                    item.Code == "DOC" ? ControlCheckpointResultStatus.Compliant : ControlCheckpointResultStatus.NonCompliant,
                    item.Code == "DOC" ? "OK" : "Nález",
                    item.RowVersion));
            res.EnsureSuccessStatusCode();
        }

        var findingRes = await _client.PostAsJsonAsync($"/api/v1/control-audits/{audit.Id}/findings",
            new AddControlFindingRequest("ACC33 zistenie", "Z UI happy-path", ControlFindingSeverity.Medium, null, null));
        findingRes.EnsureSuccessStatusCode();
        var finding = await findingRes.Content.ReadFromJsonAsync<ControlFindingDto>(JsonOptions);
        Assert.NotNull(finding);

        var actionRes = await _client.PostAsJsonAsync($"/api/v1/control-audits/findings/{finding.Id}/actions",
            new AddControlActionRequest("ACC33 opatrenie", null, null, null));
        actionRes.EnsureSuccessStatusCode();
        var action = await actionRes.Content.ReadFromJsonAsync<ControlCorrectiveActionDto>(JsonOptions);
        Assert.NotNull(action);

        (await _client.PostAsJsonAsync($"/api/v1/control-audits/actions/{action.Id}/complete",
            new CompleteControlActionRequest("Hotovo Acc33", action.RowVersion))).EnsureSuccessStatusCode();

        detail = await _client.GetFromJsonAsync<ControlAuditDetailDto>($"/api/v1/control-audits/{audit.Id}", JsonOptions);
        Assert.NotNull(detail);
        Assert.NotEmpty(detail.Findings);
        Assert.Contains(detail.Findings, f => f.Actions.Any(a => a.Status == ControlActionStatus.Completed));

        var report = await (await _client.PostAsJsonAsync($"/api/v1/control-audits/{audit.Id}/reports",
            new IssueControlReportRequest("""{"source":"Acc33"}""", detail.Audit.RowVersion)))
            .Content.ReadFromJsonAsync<ControlReportDto>(JsonOptions);
        Assert.NotNull(report);
        Assert.True(report.IsFrozen);

        detail = await _client.GetFromJsonAsync<ControlAuditDetailDto>($"/api/v1/control-audits/{audit.Id}", JsonOptions);
        Assert.NotEmpty(detail!.Reports);

        var closed = await (await _client.PostAsJsonAsync($"/api/v1/control-audits/{audit.Id}/close",
            new CloseControlAuditRequest("Uzavreté Acc33", detail.Audit.RowVersion)))
            .Content.ReadFromJsonAsync<ControlAuditDto>(JsonOptions);
        Assert.NotNull(closed);
        Assert.Equal(ControlAuditStatus.Closed, closed.Status);

        var finalDetail = await _client.GetFromJsonAsync<ControlAuditDetailDto>($"/api/v1/control-audits/{audit.Id}", JsonOptions);
        Assert.NotNull(finalDetail);
        Assert.Equal(ControlAuditStatus.Closed, finalDetail.Audit.Status);
        Assert.Equal(2, finalDetail.Checklist.Count);
        Assert.Single(finalDetail.Findings);
        Assert.Single(finalDetail.Reports);
    }

    [Fact]
    public async Task Acc34_GlobalSearch_RespectsPermissionsAndFindsSeedEntities()
    {
        await LoginAs("gestor");

        var shortQ = await _client.GetFromJsonAsync<GlobalSearchResponse>("/api/v1/search?q=x", JsonOptions);
        Assert.NotNull(shortQ);
        Assert.Empty(shortQ.Hits);

        var uas = await _client.GetFromJsonAsync<GlobalSearchResponse>("/api/v1/search?q=DEMO-UAS", JsonOptions);
        Assert.NotNull(uas);
        Assert.Contains(uas.Hits, h => h.EntityType == "Uas" && h.Title.Contains("DEMO-UAS", StringComparison.OrdinalIgnoreCase)
            || (h.EntityType == "Uas" && (h.Subtitle ?? "").Contains("DEMO-UAS", StringComparison.Ordinal)));

        var pilots = await _client.GetFromJsonAsync<GlobalSearchResponse>("/api/v1/search?q=PIL-001", JsonOptions);
        Assert.NotNull(pilots);
        Assert.Contains(pilots.Hits, h => h.EntityType == "Pilot");

        var orgs = await _client.GetFromJsonAsync<List<OrgUnitDto>>("/api/v1/org-units", JsonOptions);
        var unitA = orgs!.First(o => o.Code == "UTVAR-A");
        var opCreate = await _client.PostAsJsonAsync("/api/v1/operations", new CreateOperationRequest(
            "ACC34 search target op", "TRAINING", unitA.Id,
            DateTimeOffset.UtcNow.AddDays(1), DateTimeOffset.UtcNow.AddDays(1).AddHours(2),
            "Žilina", 49.22, 18.74, null, "Acc34", false, null));
        opCreate.EnsureSuccessStatusCode();

        var ops = await _client.GetFromJsonAsync<GlobalSearchResponse>("/api/v1/search?q=ACC34%20search", JsonOptions);
        Assert.NotNull(ops);
        Assert.Contains(ops.Hits, h => h.EntityType == "Operation");

        var accounts = await _client.GetFromJsonAsync<GlobalSearchResponse>("/api/v1/search?q=gestor", JsonOptions);
        Assert.NotNull(accounts);
        Assert.Contains(accounts.Hits, h => h.EntityType == "Account");

        await LoginAs("schvalovatel");
        var limited = await _client.GetFromJsonAsync<GlobalSearchResponse>("/api/v1/search?q=gestor", JsonOptions);
        Assert.NotNull(limited);
        Assert.DoesNotContain(limited.Hits, h => h.EntityType == "Account");
    }

    [Fact]
    public async Task Acc35_Dashboard_BranchAndUnitScopes()
    {
        await LoginAs("gestor");

        var central = await _client.GetFromJsonAsync<DashboardDto>("/api/v1/dashboard?scope=central", JsonOptions);
        Assert.NotNull(central);
        Assert.Equal("central", central.Scope);
        Assert.True(central.Fleet.TotalUas >= 2);

        var orgs = await _client.GetFromJsonAsync<List<OrgUnitDto>>("/api/v1/org-units", JsonOptions);
        Assert.NotNull(orgs);
        var branch = orgs.First(o => o.Code == "ZLOZKA-PZ");
        Assert.Equal(1, branch.Level);
        var unitA = orgs.First(o => o.Code == "UTVAR-A");
        var unitB = orgs.First(o => o.Code == "UTVAR-B");
        Assert.Equal(2, unitA.Level);
        Assert.Equal(branch.Id, unitA.ParentId);
        Assert.StartsWith(branch.Path, unitA.Path, StringComparison.Ordinal);
        Assert.StartsWith(branch.Path, unitB.Path, StringComparison.Ordinal);

        var branchDash = await _client.GetFromJsonAsync<DashboardDto>(
            $"/api/v1/dashboard?scope=branch&orgUnitId={branch.Id}", JsonOptions);
        Assert.NotNull(branchDash);
        Assert.Equal("branch", branchDash.Scope);
        Assert.Equal(branch.Id, branchDash.ScopeOrgUnitId);
        Assert.True(branchDash.Fleet.TotalUas >= 2);
        // Central may include UAS without OwningOrgUnit; branch scope does not.
        Assert.True(central.Fleet.TotalUas >= branchDash.Fleet.TotalUas);
        var branchBefore = branchDash.Fleet.TotalUas;

        var beforeA = (await _client.GetFromJsonAsync<DashboardDto>(
            $"/api/v1/dashboard?scope=unit&orgUnitId={unitA.Id}", JsonOptions))!.Fleet.TotalUas;

        var createB = await _client.PostAsJsonAsync("/api/v1/uas", new CreateUasRequest(
            "SN-ACC35-B-" + Guid.NewGuid().ToString("N")[..8], "DJI", "Mini4", null, unitB.Id, null));
        createB.EnsureSuccessStatusCode();

        var afterA = await _client.GetFromJsonAsync<DashboardDto>(
            $"/api/v1/dashboard?scope=unit&orgUnitId={unitA.Id}", JsonOptions);
        Assert.NotNull(afterA);
        Assert.Equal("unit", afterA.Scope);
        Assert.Equal(beforeA, afterA.Fleet.TotalUas);

        var afterB = await _client.GetFromJsonAsync<DashboardDto>(
            $"/api/v1/dashboard?scope=unit&orgUnitId={unitB.Id}", JsonOptions);
        Assert.NotNull(afterB);
        Assert.True(afterB.Fleet.TotalUas >= 1);

        var branchAfter = await _client.GetFromJsonAsync<DashboardDto>(
            $"/api/v1/dashboard?scope=branch&orgUnitId={branch.Id}", JsonOptions);
        Assert.NotNull(branchAfter);
        Assert.Equal(branchBefore + 1, branchAfter.Fleet.TotalUas);
    }

    [Fact]
    public async Task Acc36_SsoDevCallback_Reauth_AndSecurityHeaders()
    {
        var headersProbe = await _client.GetAsync("/api/v1/auth/sso/start");
        Assert.True(headersProbe.Headers.TryGetValues("X-Content-Type-Options", out var xcto));
        Assert.Contains("nosniff", xcto);

        var sso = await _client.GetFromJsonAsync<SsoCallbackResponse>(
            "/api/v1/auth/sso/callback?code=dev-sso:gestor&state=/", JsonOptions);
        Assert.NotNull(sso);
        Assert.False(string.IsNullOrWhiteSpace(sso.AccessToken));
        Assert.Equal("gestor", sso.UserName);

        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", sso.AccessToken);
        var dash = await _client.GetAsync("/api/v1/dashboard?scope=central");
        dash.EnsureSuccessStatusCode();

        var reauthResp = await _client.PostAsJsonAsync("/api/v1/auth/reauth",
            new ReauthRequest("Heslo123!"));
        reauthResp.EnsureSuccessStatusCode();
        var reauth = await reauthResp.Content.ReadFromJsonAsync<ReauthResponse>(JsonOptions);
        Assert.NotNull(reauth);
        Assert.False(string.IsNullOrWhiteSpace(reauth.ReauthToken));
        Assert.True(reauth.ExpiresAt > DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task Acc37_WeatherShmuSnapshot_HasHourlySeries()
    {
        await LoginAs("gestor");
        var weather = await _client.GetFromJsonAsync<WeatherSnapshotDto>("/api/v1/weather", JsonOptions);
        Assert.NotNull(weather);
        Assert.Equal("OfflineShmu", weather.ProviderCode);
        Assert.Contains("SHMÚ", weather.SourceLabel, StringComparison.OrdinalIgnoreCase);
        Assert.False(string.IsNullOrWhiteSpace(weather.StationName));
        Assert.NotEmpty(weather.Hourly);
        Assert.True(weather.Hourly.Count >= 12);
        Assert.True(weather.ValidUntil > weather.ObservedAt);
    }

    private async Task LoginAs(string userName)
    {
        _client.DefaultRequestHeaders.Authorization = null;
        var response = await _client.PostAsJsonAsync("/api/v1/auth/login",
            new LoginRequest(userName, "Heslo123!"));
        response.EnsureSuccessStatusCode();
        var login = await response.Content.ReadFromJsonAsync<LoginResponse>(JsonOptions);
        Assert.NotNull(login);
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", login.AccessToken);
    }
}

public sealed class IsSuasWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _dbName = "IsSuasTests_" + Guid.NewGuid().ToString("N");

    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        builder.UseSetting("Database:UseInMemory", "true");
        builder.UseSetting("Database:InMemoryName", _dbName);
        builder.UseSetting("Jwt:SigningKey", "TEST_SIGNING_KEY_MUST_BE_AT_LEAST_32_CHARS_LONG");
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Database:UseInMemory"] = "true",
                ["Database:InMemoryName"] = _dbName,
                ["Jwt:SigningKey"] = "TEST_SIGNING_KEY_MUST_BE_AT_LEAST_32_CHARS_LONG",
                ["Background:Enabled"] = "false"
            });
        });
    }
}
