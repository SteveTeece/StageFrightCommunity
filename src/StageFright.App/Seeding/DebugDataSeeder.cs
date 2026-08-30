using Microsoft.Extensions.Logging;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Enums;
using StageFright.Core.Localization;
using StageFright.Core.Modules.Agm;
using StageFright.Core.Modules.AuditTrail;
using StageFright.Core.Modules.Events;
using StageFright.Core.Modules.Finance;
using StageFright.Core.Modules.Members;
using StageFright.Core.Modules.Rehearsals;

namespace StageFright.App.Seeding;

/// <summary>
/// Seeds 2025–2026 of realistic small-NFP performing-group data after the first-run wizard
/// completes: 51 members (43 active/3 inactive/5 archived), a petty-cash + single bank-account
/// chart of accounts, 40 Monday-night rehearsals per year during NSW school terms with
/// probabilistic attendance, annual subscription fees, a July Eisteddfod, a September
/// Maclean/Yamba concert weekend, an annual raffle, an AGM each year in late October with
/// realistic (not full-house) attendance, a mid-term committee resignation and special
/// election, and a spread of dated operating expenses (insurance, musical director, hall hire,
/// costumes, licensing, printing, bank fees). The dataset's "as of" point is the real date the
/// seeder runs (<see cref="SeedCurrentDate"/>): anything dated on or before it is fully settled
/// — attendance taken, fees paid (bar the two deliberate non-payers), events held, expenses
/// posted — while anything after it is only put on the calendar (future rehearsals and the
/// spring concert are scheduled with no attendance; the late-October AGM sits on the calendar
/// as scheduled until its date passes, then is recorded). 2025 is therefore always a complete
/// past year and 2026 is however far along "today" is. The three most-recently-held 2026
/// rehearsals model a recent turnout dip: a flat 65% per-member attendance chance rather than
/// the usual 85–100% profile rate. Before any of that it stamps a generated organisation name
/// ("Clarence Valley Community Choir") and fee schedule — annual $120, per-rehearsal $2, a
/// February membership renewal, an October committee renewal and a six-seat general committee —
/// over whatever the setup wizard captured, so the sample dataset is internally consistent no
/// matter what placeholder figures were typed to get past the wizard's own validation.
/// Currency, language and sales-tax treatment are the exception: those follow the
/// organisation's configured settings (defaults: AUD, Australian English, no tax). Only runs
/// when the user opts in via the setup wizard checkbox. The whole run is wrapped in an
/// AuditTrailSuppressionScope — seeded records
/// are a synthetic starting fixture, not real user actions, so they produce no audit trail
/// entries (issue #296).
/// </summary>
public class DebugDataSeeder : IDebugDataSeeder
{
    private const decimal PettyCashFloat = 50m;

    /// <summary>
    /// The dataset's "as of" point — the real date the seeder runs. Anything dated on or
    /// before it is treated as already happened (attendance taken, fees paid, events held,
    /// expenses posted); anything after it is only scheduled. Using the real date keeps the
    /// sample data realistic whenever it is generated — a rehearsal, concert or AGM that has
    /// not occurred yet is on the calendar but never marked as held. The seeded term calendar
    /// only covers 2025–2026, so the seeder is meant to be run during (or after) that window.
    /// </summary>
    private static readonly DateTime SeedCurrentDate = DateTime.UtcNow.Date;

    private readonly IMemberService _memberService;
    private readonly IMemberRepository _memberRepository;
    private readonly IRehearsalService _rehearsalService;
    private readonly IAttendanceService _attendanceService;
    private readonly IEventTypeService _eventTypeService;
    private readonly IEventService _eventService;
    private readonly ICommitteeOfficeHolderTypeService _officeHolderTypeService;
    private readonly IAgmService _agmService;
    private readonly ICommitteeService _committeeService;
    private readonly IPaymentService _paymentService;
    private readonly IFeeRepository _feeRepository;
    private readonly IGLRepository _glRepository;
    private readonly IJournalEntryRepository _journalEntryRepository;
    private readonly IAccountService _accountService;
    private readonly IIncomeEntryService _incomeEntryService;
    private readonly IExpensePaymentService _expensePaymentService;
    private readonly IBankDepositService _bankDepositService;
    private readonly IOpeningBalanceService _openingBalanceService;
    private readonly ISettingsService _settingsService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<DebugDataSeeder> _logger;

    public DebugDataSeeder(
        IMemberService memberService,
        IMemberRepository memberRepository,
        IRehearsalService rehearsalService,
        IAttendanceService attendanceService,
        IEventTypeService eventTypeService,
        IEventService eventService,
        ICommitteeOfficeHolderTypeService officeHolderTypeService,
        IAgmService agmService,
        ICommitteeService committeeService,
        IPaymentService paymentService,
        IFeeRepository feeRepository,
        IGLRepository glRepository,
        IJournalEntryRepository journalEntryRepository,
        IAccountService accountService,
        IIncomeEntryService incomeEntryService,
        IExpensePaymentService expensePaymentService,
        IBankDepositService bankDepositService,
        IOpeningBalanceService openingBalanceService,
        ISettingsService settingsService,
        IUnitOfWork unitOfWork,
        ILogger<DebugDataSeeder> logger)
    {
        _memberService = memberService;
        _memberRepository = memberRepository;
        _rehearsalService = rehearsalService;
        _attendanceService = attendanceService;
        _eventTypeService = eventTypeService;
        _eventService = eventService;
        _officeHolderTypeService = officeHolderTypeService;
        _agmService = agmService;
        _committeeService = committeeService;
        _paymentService = paymentService;
        _feeRepository = feeRepository;
        _glRepository = glRepository;
        _journalEntryRepository = journalEntryRepository;
        _accountService = accountService;
        _incomeEntryService = incomeEntryService;
        _expensePaymentService = expensePaymentService;
        _bankDepositService = bankDepositService;
        _openingBalanceService = openingBalanceService;
        _settingsService = settingsService;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task SeedAsync(IProgress<string>? progress = null, CancellationToken ct = default)
    {
        var existing = await _memberService.GetByStatusAsync(MemberStatus.Active, ct);
        if (existing.Count > 0)
        {
            _logger.LogInformation("Debug seed data already present ({Count} members); skipping.", existing.Count);
            return;
        }

        // Seeded records are a synthetic starting fixture, not something a real user did —
        // suppress audit trail writes for the whole run (issue #296). The `using` guarantees
        // suppression lifts even if a step below throws partway through.
        using var _ = AuditTrailSuppressionScope.Begin();

        _logger.LogInformation("Seeding debug data...");

        progress?.Report("Creating chart of accounts…");
        var bankAccount = await _accountService.CreateAsync("Community Bank Account", AccountType.Asset, isBankAccount: true, ct: ct);
        // Must be created before any other Income account — FeeService/AttendanceService post
        // member fee income to the first non-system Income account found.
        var membershipIncomeAccount = await _accountService.CreateAsync("Membership & Attendance Fees", AccountType.Income, ct: ct);
        var concertIncomeAccount = await _accountService.CreateAsync("Concert & Ticket Sales", AccountType.Income, ct: ct);
        var raffleIncomeAccount = await _accountService.CreateAsync("Raffle Income", AccountType.Income, ct: ct);
        var insuranceExpense = await _accountService.CreateAsync("Insurance", AccountType.Expense, ct: ct);
        var musicalDirectorExpense = await _accountService.CreateAsync("Musical Director Fees", AccountType.Expense, ct: ct);
        var hallHireExpense = await _accountService.CreateAsync("Hall Hire", AccountType.Expense, ct: ct);
        var costumesExpense = await _accountService.CreateAsync("Costumes & Props", AccountType.Expense, ct: ct);
        var musicLicensingExpense = await _accountService.CreateAsync("Sheet Music & Licensing", AccountType.Expense, ct: ct);
        var printingExpense = await _accountService.CreateAsync("Printing & Stationery", AccountType.Expense, ct: ct);
        var eisteddfodEntryExpense = await _accountService.CreateAsync("Eisteddfod Entry Fees", AccountType.Expense, ct: ct);
        var bankFeesExpense = await _accountService.CreateAsync("Bank Fees", AccountType.Expense, ct: ct);

        progress?.Report("Creating 51 members…");
        var (activeMembers, inactiveMembers, archivedMembers) = await CreateMembersAsync(ct);
        _logger.LogInformation(
            "Created {Active} active, {Inactive} inactive, {Archived} archived members.",
            activeMembers.Count, inactiveMembers.Count, archivedMembers.Count);

        var eventTypes = await _eventTypeService.GetAllAsync(ct);
        var eisteddfodType = eventTypes.First(et => et.Name == "Eisteddfod");
        var performanceType = eventTypes.First(et => et.Name == "Performance");

        var officeHolderTypes = await _officeHolderTypeService.GetActiveAsync(ct);
        var presidentType = officeHolderTypes.First(t => t.Name == "President");
        var secretaryType = officeHolderTypes.First(t => t.Name == "Secretary");
        var treasurerType = officeHolderTypes.First(t => t.Name == "Treasurer");

        var settings = await _settingsService.GetAsync(ct)
            ?? throw new InvalidOperationException("Settings must be initialised before seeding debug data.");

        progress?.Report("Applying generated organisation settings…");
        await ApplyGeneratedOrganisationSettingsAsync(settings, ct);

        var random = new Random(20250101); // fixed seed — a given run date reproduces the same dataset
        var attendanceProfile = BuildAttendanceProfile(activeMembers, random);

        progress?.Report("Posting opening balances…");
        // Covers the new bank account plus every eligible system account (all but the
        // Opening Balance Equity plug itself) so a sample org starts with a realistic
        // whole-of-chart position, not just a single bank figure. Bad Debt Expense is
        // deliberately left at $0 — no historical bad debt is itself a legitimate
        // starting position. Tax Collected (2310) and Tax Receivable (2320) only get a
        // balance when the coordinator actually enabled sales tax during setup.
        var openingBalanceEntries = new List<OpeningBalanceEntry>
        {
            new() { AccountId = bankAccount.Id, Amount = 2000m },
            new() { AccountId = SystemAccounts.CashId, Amount = PettyCashFloat },
            new() { AccountId = SystemAccounts.MemberReceivableId, Amount = 180m },
            new() { AccountId = SystemAccounts.AccumulatedSurplusId, Amount = 12500m }
        };
        if (settings.IsTaxApplicable)
        {
            openingBalanceEntries.Add(new() { AccountId = SystemAccounts.TaxCollectedId, Amount = 45m });
            openingBalanceEntries.Add(new() { AccountId = SystemAccounts.TaxPaidId, Amount = 20m });
        }

        await _openingBalanceService.RecordOpeningBalancesAsync(new RecordOpeningBalancesRequest
        {
            AsAtDate = Utc(2025, 1, 1),
            Entries = openingBalanceEntries
        }, ct);

        progress?.Report("Seeding historical transfers (pre-spec 009)…");
        await SeedHistoricalTransfersAsync(bankAccount, ct);

        foreach (var year in new[] { 2025, 2026 })
        {
            _logger.LogInformation("Seeding {Year}...", year);

            progress?.Report($"Seeding {year} annual fees…");
            await SeedAnnualFeesAsync(year, activeMembers, membershipIncomeAccount, settings, random, ct);

            progress?.Report($"Seeding {year} rehearsals & attendance…");
            await SeedRehearsalsAsync(year, activeMembers, attendanceProfile, bankAccount, random, ct);

            progress?.Report($"Seeding {year} Eisteddfod…");
            await SeedEisteddfodAsync(year, activeMembers, eisteddfodType, eisteddfodEntryExpense, bankAccount, ct);

            progress?.Report($"Seeding {year} annual concert…");
            await SeedConcertsAsync(year, activeMembers, performanceType, concertIncomeAccount, bankAccount, ct);

            progress?.Report($"Seeding {year} raffle…");
            await SeedRaffleAsync(year, raffleIncomeAccount, ct);

            progress?.Report($"Seeding {year} AGM…");
            var agm = await SeedAgmAsync(year, activeMembers, presidentType.Id, secretaryType.Id, treasurerType.Id, random, ct);

            if (year == 2025 && agm is not null)
            {
                progress?.Report("Seeding mid-term committee resignation…");
                await SeedSpecialElectionAsync(agm.Id, activeMembers, ct);
            }

            progress?.Report($"Seeding {year} operating expenses…");
            await SeedOperatingExpensesAsync(
                year, bankAccount, hallHireExpense, costumesExpense, musicLicensingExpense,
                printingExpense, insuranceExpense, musicalDirectorExpense, bankFeesExpense, ct);
        }

        progress?.Report("Seed complete!");
        _logger.LogInformation(
            "Debug data seed complete — {Active} active, {Inactive} inactive, {Archived} archived members, 2 years.",
            activeMembers.Count, inactiveMembers.Count, archivedMembers.Count);
    }

    // -------------------------------------------------------------------------
    // Generated organisation settings
    // -------------------------------------------------------------------------

    /// <summary>
    /// Stamps a generated organisation name and fee schedule over whatever the setup wizard
    /// captured, so the sample dataset is internally consistent no matter what placeholder
    /// figures were typed to satisfy the wizard's Organisation Settings tab. The fee amounts
    /// are what the rest of the run reads: <see cref="SeedAnnualFeesAsync"/> is a no-op at a
    /// zero annual fee, and the door-cash → petty-cash → bank sweep in
    /// <see cref="SeedRehearsalsAsync"/> moves nothing at a zero attendance fee. The renewal
    /// months and committee-seat target line up with the seeded February Term 1 fee run, the
    /// late-October AGM (<see cref="GetAgmDate"/>) and the six general committee members
    /// <see cref="SeedAgmAsync"/> elects at it. Currency, language, theme, sales-tax treatment,
    /// financial-year start, inception date, audit retention and age ranges are deliberately
    /// left as the coordinator configured them. The <see cref="ISettingsService.SaveAsync"/>
    /// audit entry is suppressed like every other seeded write by the enclosing
    /// <see cref="AuditTrailSuppressionScope"/>.
    /// </summary>
    private async Task ApplyGeneratedOrganisationSettingsAsync(Settings settings, CancellationToken ct)
    {
        settings.OrganizationName = "Clarence Valley Community Choir";
        settings.AnnualFee = 120m;
        settings.AttendanceFee = 2m;
        settings.MembershipRenewalMonth = 2;          // February — collected at the first Term 1 rehearsal
        settings.CommitteeRenewalMonth = 10;          // October — matches GetAgmDate
        settings.GeneralCommitteeSeatCountTarget = 6; // matches SeedAgmAsync's six general members

        await _settingsService.SaveAsync(settings, ct);
    }

    // -------------------------------------------------------------------------
    // Members
    // -------------------------------------------------------------------------

    private async Task<(IReadOnlyList<Member> Active, IReadOnlyList<Member> Inactive, IReadOnlyList<Member> Archived)>
        CreateMembersAsync(CancellationToken ct)
    {
        // 51 members with realistic Clarence Valley NSW details. All joined before 2025 —
        // the first 43 are Active throughout 2025–2026, the next 3 are Inactive, and the
        // last 5 are Archived (soft-deleted); none change status during the seeded window.
        // DateOfBirth values give each member an age between 15 and 93 years (as of the
        // seed's reference "today" of 1 July 2026 — see SeedCurrentDate).
        (string FirstName, string LastName, string Address, string Phone, string Email, DateTime JoinDate, DateTime DateOfBirth)[] data =
        [
            ("Margaret", "Thompson",  "12 Clarence St, Maclean NSW 2463",        "02 6645 1234", "margaret.thompson@example.com",   new DateTime(2015, 3, 10), new DateTime(1948, 1, 10)),
            ("Patricia", "Robinson",  "5 River Rd, Maclean NSW 2463",             "02 6645 2345", "patricia.robinson@example.com",   new DateTime(2016, 2,  1), new DateTime(1952, 2, 11)),
            ("Susan", "Williams",     "88 Church St, Maclean NSW 2463",           "02 6645 3456", "susan.williams@example.com",      new DateTime(2014, 7, 15), new DateTime(1945, 3, 12)),
            ("Barbara", "Taylor",     "3 Cameron Dr, Maclean NSW 2463",           "02 6645 4567", "barbara.taylor@example.com",      new DateTime(2017, 4, 22), new DateTime(1957, 4, 13)),
            ("Dorothy", "Anderson",   "41 Elm Ave, Maclean NSW 2463",             "02 6645 5678", "dorothy.anderson@example.com",    new DateTime(2013, 9,  5), new DateTime(1941, 5, 14)),
            ("Elizabeth", "Harris",   "7 Pacific Hwy, Maclean NSW 2463",          "02 6645 6789", "elizabeth.harris@example.com",    new DateTime(2018, 1, 30), new DateTime(1954, 6, 15)),
            ("Carol", "Martin",       "22 Oak St, Lawrence NSW 2460",             "02 6647 7890", "carol.martin@example.com",        new DateTime(2015, 6, 14), new DateTime(1960, 7, 16)),
            ("Helen", "Wilson",       "9 Palmers Island Rd, Lawrence NSW 2460",   "02 6647 8901", "helen.wilson@example.com",        new DateTime(2016, 11, 3), new DateTime(1956, 8, 17)),
            ("Catherine", "Moore",    "66 Station St, Grafton NSW 2460",          "02 6642 9012", "catherine.moore@example.com",     new DateTime(2019, 2, 28), new DateTime(1968, 9, 18)),
            ("Frances", "Davis",      "14 Crown St, Grafton NSW 2460",            "02 6642 0123", "frances.davis@example.com",       new DateTime(2014, 8, 19), new DateTime(1943, 10, 19)),
            ("Linda", "Johnson",      "33 Villiers St, Grafton NSW 2460",         "02 6642 1234", "linda.johnson@example.com",       new DateTime(2017, 5,  7), new DateTime(1965, 11, 20)),
            ("Ruth", "Mitchell",      "5 Newman St, Yamba NSW 2464",              "02 6646 2345", "ruth.mitchell@example.com",       new DateTime(2020, 1, 15), new DateTime(1971, 12, 21)),
            ("Joan", "Lewis",         "18 Yamba Rd, Yamba NSW 2464",              "02 6646 3456", "joan.lewis@example.com",          new DateTime(2015, 10, 22), new DateTime(1950, 1, 22)),
            ("Shirley", "Walker",     "42 Coldstream St, Yamba NSW 2464",         "02 6646 4567", "shirley.walker@example.com",      new DateTime(2016, 3,  8), new DateTime(1958, 2, 23)),
            ("Anne", "Campbell",      "91 River St, Yamba NSW 2464",              "02 6646 5678", "anne.campbell@example.com",       new DateTime(2018, 7, 14), new DateTime(1963, 3, 24)),
            ("Michelle", "Lee",       "28 Flinders St, Iluka NSW 2454",           "02 6646 6789", "michelle.lee@example.com",        new DateTime(2021, 2,  1), new DateTime(1979, 4, 10)),
            ("Amanda", "Scott",       "15 Charles St, Iluka NSW 2454",            "02 6646 7890", "amanda.scott@example.com",        new DateTime(2019, 9, 30), new DateTime(1974, 5, 11)),
            ("Stephanie", "Hall",     "7 Solitary Is Way, Wooli NSW 2462",        "02 6649 8901", "stephanie.hall@example.com",      new DateTime(2022, 1, 10), new DateTime(1988, 6, 12)),
            ("Rachel", "Young",       "3 Orara St, Grafton NSW 2460",             "02 6642 9012", "rachel.young@example.com",        new DateTime(2020, 6,  5), new DateTime(1982, 7, 13)),
            ("Karen", "Roberts",      "55 Pound St, Grafton NSW 2460",            "02 6642 0234", "karen.roberts@example.com",       new DateTime(2017, 12, 1), new DateTime(1967, 8, 14)),
            ("Robert", "Smith",       "10 Main St, Maclean NSW 2463",             "02 6645 1111", "robert.smith@example.com",        new DateTime(2015, 1, 20), new DateTime(1953, 9, 15)),
            ("John", "Davies",        "44 Park Ave, Maclean NSW 2463",            "02 6645 2222", "john.davies@example.com",         new DateTime(2016, 4, 12), new DateTime(1959, 10, 16)),
            ("William", "Evans",      "6 Queen St, Maclean NSW 2463",             "02 6645 3333", "william.evans@example.com",       new DateTime(2013, 11, 28), new DateTime(1938, 11, 17)),
            ("David", "Clark",        "19 Wharf St, Maclean NSW 2463",            "02 6645 4444", "david.clark@example.com",         new DateTime(2018, 8,  3), new DateTime(1966, 12, 18)),
            ("James", "Hughes",       "31 Bruce Hwy, Lawrence NSW 2460",          "02 6647 5555", "james.hughes@example.com",        new DateTime(2014, 5, 17), new DateTime(1947, 1, 19)),
            ("Michael", "Turner",     "8 Cooper Rd, Lawrence NSW 2460",           "02 6647 6666", "michael.turner@example.com",      new DateTime(2017, 3, 25), new DateTime(1969, 2, 20)),
            ("Thomas", "Baker",       "52 Tinonee Rd, Maclean NSW 2463",          "02 6645 7777", "thomas.baker@example.com",        new DateTime(2016, 7,  9), new DateTime(1955, 3, 21)),
            ("Christopher", "Morris", "17 Orara Way, Grafton NSW 2460",           "02 6642 8888", "christopher.morris@example.com",  new DateTime(2019, 11, 16), new DateTime(1977, 4, 22)),
            ("Andrew", "Price",       "4 Fitzroy St, Grafton NSW 2460",           "02 6642 9999", "andrew.price@example.com",        new DateTime(2021, 4,  1), new DateTime(1985, 5, 23)),
            ("Kenneth", "Bennett",    "28 Mary St, Grafton NSW 2460",             "02 6642 0001", "kenneth.bennett@example.com",     new DateTime(2015, 9, 14), new DateTime(1951, 6, 24)),
            ("Steven", "Cook",        "73 Duke St, Grafton NSW 2460",             "02 6642 1112", "steven.cook@example.com",         new DateTime(2018, 2, 20), new DateTime(1972, 7, 10)),
            ("Gregory", "Collins",    "11 Yamba Rd, Yamba NSW 2464",              "02 6646 2223", "gregory.collins@example.com",     new DateTime(2014, 12, 6), new DateTime(1944, 8, 11)),
            ("Paul", "Ward",          "36 Coldstream St, Yamba NSW 2464",         "02 6646 3334", "paul.ward@example.com",           new DateTime(2017, 6, 18), new DateTime(1961, 9, 12)),
            ("Brian", "James",        "22 The Esplanade, Yamba NSW 2464",         "02 6646 4445", "brian.james@example.com",         new DateTime(2020, 3, 11), new DateTime(1980, 10, 13)),
            ("Kevin", "Phillips",     "5 Carrs Dr, Yamba NSW 2464",               "02 6646 5556", "kevin.phillips@example.com",      new DateTime(2016, 10, 24), new DateTime(1957, 11, 14)),
            ("Edward", "Stewart",     "14 Wooli St, Wooli NSW 2462",              "02 6649 6667", "edward.stewart@example.com",      new DateTime(2019, 7,  8), new DateTime(1975, 12, 15)),
            ("Ronald", "Thomson",     "8 Pacific Pde, Wooli NSW 2462",            "02 6649 7778", "ronald.thomson@example.com",      new DateTime(2022, 5, 15), new DateTime(1993, 1, 16)),
            ("Daniel", "White",       "29 Sandy Beach Rd, Wooli NSW 2462",        "02 6649 8889", "daniel.white@example.com",        new DateTime(2013, 8, 27), new DateTime(1935, 2, 17)),
            ("Mark", "Green",         "17 Palmers Island Rd, Grafton NSW 2460",   "02 6642 9990", "mark.green@example.com",          new DateTime(2021, 1,  4), new DateTime(1990, 3, 18)),
            ("Timothy", "Hill",       "6 South Arm Rd, Brushgrove NSW 2460",      "02 6647 0001", "timothy.hill@example.com",        new DateTime(2018, 4, 16), new DateTime(1970, 4, 19)),
            // Additional Active members (41–43)
            ("Emma", "Nguyen",        "21 Yamba Rd, Yamba NSW 2464",              "02 6646 1123", "emma.nguyen@example.com",         new DateTime(2016, 5, 12), new DateTime(1997, 5, 20)),
            ("Liam", "Fitzgerald",    "9 Prince St, Grafton NSW 2460",            "02 6642 2234", "liam.fitzgerald@example.com",     new DateTime(2018, 9,  3), new DateTime(2002, 6, 21)),
            ("Sophie", "Chen",        "14 River St, Maclean NSW 2463",           "02 6645 3345", "sophie.chen@example.com",         new DateTime(2019, 11, 20), new DateTime(2009, 7, 22)),
            // Inactive members (44–46)
            ("Harold", "Bishop",      "6 Ferry St, Ulmarra NSW 2462",             "02 6644 4456", "harold.bishop@example.com",       new DateTime(2013, 2, 14), new DateTime(1937, 8, 23)),
            ("Nancy", "Fraser",       "3 Coldstream St, Yamba NSW 2464",          "02 6646 5567", "nancy.fraser@example.com",        new DateTime(2014, 6,  8), new DateTime(1949, 9, 24)),
            ("Victor", "Osborne",     "18 Bent St, Chatsworth Island NSW 2469",   "02 6645 6678", "victor.osborne@example.com",      new DateTime(2012, 10, 30), new DateTime(1933, 10, 10)),
            // Archived members (47–51)
            ("Beatrice", "Sinclair",  "27 Yamba Rd, Yamba NSW 2464",              "02 6646 7789", "beatrice.sinclair@example.com",   new DateTime(2011, 3, 17), new DateTime(1936, 11, 11)),
            ("Gerald", "Whitfield",   "5 Spenser St, Grafton NSW 2460",           "02 6642 8890", "gerald.whitfield@example.com",    new DateTime(2012, 7, 22), new DateTime(1942, 12, 12)),
            ("Ivy", "Sorenson",       "12 Ashby Island Rd, Ashby NSW 2463",       "02 6645 9901", "ivy.sorenson@example.com",        new DateTime(2013, 1,  9), new DateTime(1939, 1, 13)),
            ("Norman", "Blackwood",   "44 Pound St, Grafton NSW 2460",            "02 6642 0012", "norman.blackwood@example.com",    new DateTime(2010, 12, 4), new DateTime(1934, 2, 14)),
            ("Agnes", "Pemberton",    "8 Townsend Rd, Townsend NSW 2463",         "02 6645 1129", "agnes.pemberton@example.com",     new DateTime(2014, 4, 27), new DateTime(1946, 3, 15)),
        ];

        var all = new List<Member>(data.Length);
        foreach (var (firstName, lastName, address, phone, email, joinDate, dateOfBirth) in data)
        {
            var member = await _memberService.CreateAsync(new CreateMemberRequest
            {
                FirstName = firstName,
                LastName = lastName,
                StreetAddress = address,
                Phone = phone,
                Email = email,
                JoinDate = joinDate,
                DateOfBirth = dateOfBirth
            }, ct);

            // MemberService.CreateAsync stamps ActivateDate with today's real wall-clock date.
            // Backdate it to JoinDate so GetActiveAsOfAsync (used to freeze attendance/
            // participation rates) correctly counts these members as active throughout 2025–2026.
            member.ActivateDate = joinDate;
            await _memberRepository.UpdateAsync(member, ct);

            all.Add(member);
        }

        var active = all.Take(43).ToList();
        var inactive = all.Skip(43).Take(3).ToList();
        var archived = all.Skip(46).Take(5).ToList();

        foreach (var member in inactive)
            await _memberService.InactivateAsync(member.Id, ct);

        foreach (var member in archived)
            await _memberService.ArchiveAsync(member.Id, ct);

        return (active, inactive, archived);
    }

    // -------------------------------------------------------------------------
    // Attendance modelling
    // -------------------------------------------------------------------------

    /// <summary>
    /// 25% of active members attend every rehearsal; the rest attend at an individually
    /// assigned rate of 85–90%.
    /// </summary>
    private static Dictionary<Guid, double> BuildAttendanceProfile(IReadOnlyList<Member> activeMembers, Random random)
    {
        var alwaysCount = (int)Math.Round(activeMembers.Count * 0.25);
        var shuffled = activeMembers.OrderBy(_ => random.Next()).ToList();

        var profile = new Dictionary<Guid, double>();
        for (var i = 0; i < shuffled.Count; i++)
            profile[shuffled[i].Id] = i < alwaysCount ? 1.0 : 0.85 + random.NextDouble() * 0.05;

        return profile;
    }

    // -------------------------------------------------------------------------
    // Historical transfers (pre-spec 009 regression testing)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Seeds a few historical Transfer entries (JournalEntryType.Transfer) to test regression
    /// scenarios. These predate the spec 009 refactor that replaced the generic Transfer page
    /// with a dedicated BankDeposit workflow. Verifies they still display correctly in reports.
    /// </summary>
    private async Task SeedHistoricalTransfersAsync(Account bankAccount, CancellationToken ct)
    {
        var historyDate1 = Utc(2025, 1, 15);
        var historyDate2 = Utc(2025, 3, 10);

        if (historyDate1 > SeedCurrentDate)
            return; // too far in the future — no transfers to seed

        await _unitOfWork.ExecuteInTransactionAsync(async innerCt =>
        {
            var now = DateTime.UtcNow;

            // Historical transfer 1: small transfer between bank accounts (old workflow)
            var entry1 = new JournalEntry
            {
                Id = Guid.NewGuid(),
                Type = JournalEntryType.Transfer,
                Date = historyDate1,
                Description = "Historical transfer: Float sweep to bank",
                CreatedAt = now
            };
            var savedEntry1 = await _journalEntryRepository.AddAsync(entry1, innerCt);

            await _glRepository.AddBalancedSetAsync(new[]
            {
                new Transaction
                {
                    Id = Guid.NewGuid(),
                    Date = historyDate1,
                    AccountId = bankAccount.Id,
                    DebitAmount = 150m,
                    CreditAmount = 0m,
                    GLAccount = bankAccount.AccountNumber,
                    JournalEntryId = savedEntry1.Id,
                    Description = "Historical transfer: Float sweep to bank",
                    CreatedAt = now
                },
                new Transaction
                {
                    Id = Guid.NewGuid(),
                    Date = historyDate1,
                    AccountId = SystemAccounts.CashId,
                    DebitAmount = 0m,
                    CreditAmount = 150m,
                    GLAccount = SystemAccounts.CashNumber,
                    JournalEntryId = savedEntry1.Id,
                    Description = "Historical transfer: Float sweep to bank",
                    CreatedAt = now
                }
            }, innerCt);

            // Historical transfer 2: larger transfer from bank to cash (cash withdrawal)
            if (historyDate2 <= SeedCurrentDate)
            {
                var entry2 = new JournalEntry
                {
                    Id = Guid.NewGuid(),
                    Type = JournalEntryType.Transfer,
                    Date = historyDate2,
                    Description = "Historical transfer: Cash withdrawal from bank",
                    CreatedAt = now
                };
                var savedEntry2 = await _journalEntryRepository.AddAsync(entry2, innerCt);

                await _glRepository.AddBalancedSetAsync(new[]
                {
                    new Transaction
                    {
                        Id = Guid.NewGuid(),
                        Date = historyDate2,
                        AccountId = SystemAccounts.CashId,
                        DebitAmount = 500m,
                        CreditAmount = 0m,
                        GLAccount = SystemAccounts.CashNumber,
                        JournalEntryId = savedEntry2.Id,
                        Description = "Historical transfer: Cash withdrawal from bank",
                        CreatedAt = now
                    },
                    new Transaction
                    {
                        Id = Guid.NewGuid(),
                        Date = historyDate2,
                        AccountId = bankAccount.Id,
                        DebitAmount = 0m,
                        CreditAmount = 500m,
                        GLAccount = bankAccount.AccountNumber,
                        JournalEntryId = savedEntry2.Id,
                        Description = "Historical transfer: Cash withdrawal from bank",
                        CreatedAt = now
                    }
                }, innerCt);
            }
        }, ct);

        _logger.LogInformation("Seeded 2 historical Transfer entries for regression testing (spec 009).");
    }

    // -------------------------------------------------------------------------
    // Annual fees
    // -------------------------------------------------------------------------

    /// <summary>
    /// 90% of active members pay their annual subscription in cash by the 2nd week of
    /// February; two members pay late in the year and two remain unpaid (exercises
    /// aging/outstanding balance reporting). The same four members lag in both years.
    /// Like attendance fees, the cash is handed over at a rehearsal and collected into
    /// petty cash — it rides the same door-to-bank sweep as attendance fee cash rather
    /// than being deposited on an arbitrary calendar date.
    /// </summary>
    private async Task SeedAnnualFeesAsync(
        int year,
        IReadOnlyList<Member> activeMembers,
        Account incomeAccount,
        Settings settings,
        Random random,
        CancellationToken ct)
    {
        if (settings.AnnualFee <= 0m) return;

        var feeDate = Utc(year, 1, 1);
        var dueDate = Utc(year, 12, 31);
        var rehearsalDates = GetRehearsalDates(year).ToList();

        var lastIndex = activeMembers.Count - 1;
        var latePayerIndexes = new HashSet<int> { lastIndex, lastIndex - 1 };
        var nonPayerIndexes = new HashSet<int> { lastIndex - 2, lastIndex - 3 };

        for (var i = 0; i < activeMembers.Count; i++)
        {
            var member = activeMembers[i];
            await CreateAnnualFeeAccrualAsync(member.Id, year, feeDate, dueDate, incomeAccount, settings, ct);

            if (nonPayerIndexes.Contains(i))
                continue; // stays outstanding for the whole year

            // Cash handed over at a rehearsal — 1st/2nd rehearsal of Term 1 (by the 2nd week
            // of Feb) for on-time payers, a Term 2/early Term 3 rehearsal for late payers.
            var paymentDate = latePayerIndexes.Contains(i)
                ? rehearsalDates[random.Next(12, 26)]
                : rehearsalDates[random.Next(0, 2)];

            if (paymentDate > SeedCurrentDate)
                continue; // the rehearsal night this payment would ride in on hasn't happened yet — stays outstanding

            await _paymentService.RecordAsync(new RecordPaymentRequest
            {
                MemberId = member.Id,
                Date = paymentDate,
                Amount = settings.AnnualFee,
                PaymentMethod = PaymentMethod.Cash,
                PaymentType = PaymentType.Annual,
                Notes = $"{year} annual membership fee — paid in cash at rehearsal"
            }, ct);
        }
    }

    /// <summary>Mirrors FeeService's accrual GL pattern for an arbitrary (possibly backdated) year.</summary>
    private async Task<Fee> CreateAnnualFeeAccrualAsync(
        Guid memberId,
        int year,
        DateTime feeDate,
        DateTime dueDate,
        Account incomeAccount,
        Settings settings,
        CancellationToken ct)
    {
        var taxCode = settings.IsTaxApplicable ? (settings.AnnualFeeTaxCode ?? TaxCode.TaxExempt) : (TaxCode?)null;
        var (incomeAmount, taxAmount) = taxCode == TaxCode.Taxable
            ? TaxCalculator.SplitInclusive(settings.AnnualFee, settings.TaxRate ?? 0m,
                CurrencyCatalog.Get(settings.CurrencyCode).MinorUnitDigits)
            : (settings.AnnualFee, 0m);

        Fee savedFee = null!;
        await _unitOfWork.ExecuteInTransactionAsync(async innerCt =>
        {
            var now = DateTime.UtcNow;
            var fee = new Fee
            {
                Id = Guid.NewGuid(),
                MemberId = memberId,
                FeeType = FeeType.Annual,
                Amount = settings.AnnualFee,
                FeeDate = feeDate,
                DueDate = dueDate,
                PaidAtCreation = false,
                TaxCode = taxCode,
                CreatedAt = now
            };
            savedFee = await _feeRepository.AddAsync(fee, innerCt);

            var lines = new List<Transaction>
            {
                new()
                {
                    Id = Guid.NewGuid(), Date = feeDate,
                    AccountId = SystemAccounts.MemberReceivableId, GLAccount = SystemAccounts.MemberReceivableNumber,
                    DebitAmount = settings.AnnualFee, CreditAmount = 0m,
                    MemberId = memberId, FeeId = savedFee.Id, TaxCode = taxCode,
                    Description = $"Annual membership fee {year}", CreatedAt = now
                },
                new()
                {
                    Id = Guid.NewGuid(), Date = feeDate,
                    AccountId = incomeAccount.Id, GLAccount = incomeAccount.AccountNumber,
                    DebitAmount = 0m, CreditAmount = incomeAmount,
                    FeeId = savedFee.Id, TaxCode = taxCode,
                    Description = $"Annual membership fee income {year}", CreatedAt = now
                }
            };

            if (taxAmount != 0m)
            {
                lines.Add(new Transaction
                {
                    Id = Guid.NewGuid(), Date = feeDate,
                    AccountId = SystemAccounts.TaxCollectedId, GLAccount = SystemAccounts.TaxCollectedNumber,
                    DebitAmount = 0m, CreditAmount = taxAmount,
                    FeeId = savedFee.Id, TaxCode = taxCode,
                    Description = $"Tax collected — annual membership fee {year}", CreatedAt = now
                });
            }

            await _glRepository.AddBalancedSetAsync(lines, innerCt);
        }, ct);

        return savedFee;
    }

    // -------------------------------------------------------------------------
    // Rehearsals, attendance and petty-cash banking
    // -------------------------------------------------------------------------

    /// <summary>
    /// One rehearsal per Monday across 40 term weeks — every one is put on the schedule, but
    /// attendance and door-cash banking are recorded only for those already held (dated on or
    /// before <see cref="SeedCurrentDate"/>). Attendance fees are collected at the door into
    /// petty cash and, 2–3 days later, swept to the bank account above a small float retained
    /// in the tin. The three most-recently-held 2026 rehearsals use a flat 65% per-member
    /// attendance chance — a recent turnout dip — instead of each member's usual 85–100%
    /// profile rate.
    /// </summary>
    private async Task SeedRehearsalsAsync(
        int year,
        IReadOnlyList<Member> activeMembers,
        IReadOnlyDictionary<Guid, double> attendanceProfile,
        Account bankAccount,
        Random random,
        CancellationToken ct)
    {
        const double recentDipAttendanceRate = 0.65;
        var dates = GetRehearsalDates(year).ToList();
        // The three most-recently-held 2026 rehearsals model a recent turnout dip (see the
        // method summary). Skipped for 2025 and until at least three 2026 rehearsals are held.
        var heldCount = dates.Count(d => d <= SeedCurrentDate);
        var recentDipFromIndex = year == 2026 && heldCount >= 3 ? heldCount - 3 : int.MaxValue;

        for (var i = 0; i < dates.Count; i++)
        {
            var date = dates[i];
            var rehearsal = await _rehearsalService.ScheduleAsync(new ScheduleRehearsalRequest
            {
                Date = date,
                Time = new TimeSpan(19, 30, 0) // 7:30 PM
            }, ct);

            if (date > SeedCurrentDate)
                continue; // future rehearsal — scheduled only, attendance not yet taken

            var inRecentDip = i >= recentDipFromIndex;
            var items = activeMembers
                .Select(m => new AttendanceBatchItem
                {
                    MemberId = m.Id,
                    Attended = random.NextDouble() < (inRecentDip ? recentDipAttendanceRate : attendanceProfile[m.Id]),
                    MarkAsUnpaid = false // attendance fee collected at the door
                })
                .ToList();

            await _attendanceService.RecordBatchAsync(rehearsal.Id, items, ct);

            var sweepDate = date.AddDays(random.Next(2, 4)); // 2 or 3 days later
            var pettyCashBalance = await _glRepository.GetAccountBalanceAsync(SystemAccounts.CashId, sweepDate, ct);
            if (pettyCashBalance > PettyCashFloat)
            {
                var depositAmount = Math.Round(pettyCashBalance - PettyCashFloat, 2);
                // Most deposits include a description, but occasionally test default description handling
                var shouldUseDefaultDescription = random.NextDouble() < 0.05; // 5% of deposits use default

                await _bankDepositService.RecordDepositAsync(new RecordBankDepositRequest
                {
                    Date = sweepDate,
                    Amount = depositAmount,
                    ToAccountId = bankAccount.Id,
                    Description = shouldUseDefaultDescription ? null : "Banking of rehearsal attendance fees"
                }, ct);
            }
        }
    }

    // -------------------------------------------------------------------------
    // Eisteddfod
    // -------------------------------------------------------------------------

    private async Task SeedEisteddfodAsync(
        int year,
        IReadOnlyList<Member> activeMembers,
        EventType eisteddfodType,
        Account eisteddfodEntryExpense,
        Account bankAccount,
        CancellationToken ct)
    {
        var eventDate = GetEisteddfodDate(year);

        var evt = await _eventService.ScheduleAsync(new ScheduleEventRequest
        {
            Date = eventDate,
            EventTypeId = eisteddfodType.Id,
            Notes = $"{year} Eisteddfod"
        }, ct);

        if (eventDate <= SeedCurrentDate)
        {
            var items = activeMembers.Select(m => new ParticipationBatchItem { MemberId = m.Id, Participated = true }).ToList();
            await _eventService.RecordParticipationAsync(evt.Id, items, ct);
        }

        var entryFeeDate = eventDate.AddDays(-14);
        if (entryFeeDate > SeedCurrentDate)
            return; // entry fee hasn't been paid yet

        await _expensePaymentService.RecordExpenseAsync(new RecordExpenseRequest
        {
            Date = entryFeeDate,
            Amount = 150m,
            BankAccountId = bankAccount.Id,
            ExpenseAccountId = eisteddfodEntryExpense.Id,
            Payee = "Eisteddfod Committee",
            Description = $"{year} Eisteddfod entry fee"
        }, ct);
    }

    // -------------------------------------------------------------------------
    // Annual concert (Maclean Saturday / Yamba Sunday)
    // -------------------------------------------------------------------------

    private async Task SeedConcertsAsync(
        int year,
        IReadOnlyList<Member> activeMembers,
        EventType performanceType,
        Account concertIncomeAccount,
        Account bankAccount,
        CancellationToken ct)
    {
        var (macleanDate, yambaDate) = GetConcertDates(year);
        var participationItems = activeMembers.Select(m => new ParticipationBatchItem { MemberId = m.Id, Participated = true }).ToList();

        var macleanEvent = await _eventService.ScheduleAsync(new ScheduleEventRequest
        {
            Date = macleanDate,
            EventTypeId = performanceType.Id,
            Notes = $"{year} Annual Concert — Saturday, Maclean"
        }, ct);
        if (macleanDate <= SeedCurrentDate)
            await _eventService.RecordParticipationAsync(macleanEvent.Id, participationItems, ct);
        await RecordConcertTicketIncomeAsync(macleanDate, 560m, "Maclean", concertIncomeAccount, bankAccount, ct);

        var yambaEvent = await _eventService.ScheduleAsync(new ScheduleEventRequest
        {
            Date = yambaDate,
            EventTypeId = performanceType.Id,
            Notes = $"{year} Annual Concert — Sunday, Yamba"
        }, ct);
        if (yambaDate <= SeedCurrentDate)
            await _eventService.RecordParticipationAsync(yambaEvent.Id, participationItems, ct);
        await RecordConcertTicketIncomeAsync(yambaDate, 860m, "Yamba", concertIncomeAccount, bankAccount, ct);
    }

    /// <summary>Ticket sales are 70% cash (banked like attendance fees) and 30% direct EFTPOS.</summary>
    private async Task RecordConcertTicketIncomeAsync(
        DateTime date, decimal totalSales, string location, Account concertIncomeAccount, Account bankAccount, CancellationToken ct)
    {
        if (date > SeedCurrentDate)
            return; // concert hasn't happened yet — no ticket sales to record

        var cashAmount = Math.Round(totalSales * 0.7m, 2);
        var eftposAmount = totalSales - cashAmount;

        await _incomeEntryService.RecordIncomeAsync(new RecordIncomeRequest
        {
            Date = date,
            Amount = cashAmount,
            AccountId = concertIncomeAccount.Id,
            Description = $"{location} concert ticket sales — cash"
        }, ct);

        await _incomeEntryService.RecordIncomeAsync(new RecordIncomeRequest
        {
            Date = date,
            Amount = eftposAmount,
            AccountId = concertIncomeAccount.Id,
            DepositAccountId = bankAccount.Id,
            Description = $"{location} concert ticket sales — EFTPOS"
        }, ct);
    }

    // -------------------------------------------------------------------------
    // Raffle
    // -------------------------------------------------------------------------

    private async Task SeedRaffleAsync(int year, Account raffleIncomeAccount, CancellationToken ct)
    {
        var raffleDate = GetRaffleDate(year);
        if (raffleDate > SeedCurrentDate)
            return; // raffle hasn't been drawn yet

        var amount = year == 2025 ? 1620m : 1580m; // one raffle per year, always over $1,500

        await _incomeEntryService.RecordIncomeAsync(new RecordIncomeRequest
        {
            Date = raffleDate,
            Amount = amount,
            AccountId = raffleIncomeAccount.Id,
            Description = $"{year} annual raffle ticket sales"
        }, ct);
    }

    // -------------------------------------------------------------------------
    // Annual General Meeting
    // -------------------------------------------------------------------------

    /// <summary>
    /// Puts the AGM on the calendar via IAgmService.ScheduleAsync every year, then — once its
    /// date has passed (on or before <see cref="SeedCurrentDate"/>) — records it:
    /// President/Secretary/Treasurer (the three built-in office-holder titles) and 6 general
    /// committee members, all re-elected unopposed each year. Attendance is a realistic 70–85%
    /// turnout rather than a full house — the elected/nominated members always attend their own
    /// election, but the rest of the membership turns up at the same rate a small club AGM
    /// typically draws. Recording the AGM closes whatever committee term was previously open
    /// and starts a new one. A future AGM is returned in its scheduled, not-yet-held state
    /// (no attendance or election recorded).
    /// </summary>
    private async Task<AnnualGeneralMeeting?> SeedAgmAsync(
        int year,
        IReadOnlyList<Member> activeMembers,
        Guid presidentTypeId,
        Guid secretaryTypeId,
        Guid treasurerTypeId,
        Random random,
        CancellationToken ct)
    {
        var agmDate = GetAgmDate(year);

        var scheduled = await _agmService.ScheduleAsync(new ScheduleAgmRequest(
            agmDate, $"{year} Annual General Meeting — new committee term commences"), ct);

        // Future AGM: on the calendar, but not yet held — no attendance or election recorded.
        if (agmDate > SeedCurrentDate)
            return scheduled;

        var memberIds = activeMembers.Select(m => m.Id).ToList();
        var officeHolderAssignments = new Dictionary<Guid, Guid>
        {
            [presidentTypeId] = activeMembers[0].Id,
            [secretaryTypeId] = activeMembers[1].Id,
            [treasurerTypeId] = activeMembers[2].Id
        };
        var generalCommitteeMemberIds = activeMembers.Skip(3).Take(6).Select(m => m.Id).ToList();

        var assignedMemberIds = new HashSet<Guid>(officeHolderAssignments.Values);
        assignedMemberIds.UnionWith(generalCommitteeMemberIds);

        var attendanceRate = 0.70 + random.NextDouble() * 0.15; // 70–85% turnout, varies by year
        var attendedMemberIds = memberIds
            .Where(id => assignedMemberIds.Contains(id) || random.NextDouble() < attendanceRate)
            .ToList();

        return await _agmService.RecordAsync(scheduled.Id, new RecordAgmRequest(
            AttendedMemberIds: attendedMemberIds,
            AllActiveMemberIds: memberIds,
            OfficeHolderAssignments: officeHolderAssignments,
            GeneralCommitteeMemberIds: generalCommitteeMemberIds), ct);
    }

    /// <summary>
    /// Mid-term regression fixture: one general committee member elected at the 2025 AGM
    /// resigns partway through the term and is replaced by special election ahead of the
    /// 2026 AGM. A single unbroken committee term never exercises AgmDetail's dated,
    /// multi-holder rendering for a position (FR-029) — this gives it a seeded example.
    /// </summary>
    private async Task SeedSpecialElectionAsync(Guid agmId, IReadOnlyList<Member> activeMembers, CancellationToken ct)
    {
        var replacementDate = Utc(2026, 3, 16);
        if (replacementDate > SeedCurrentDate)
            return;

        var positions = await _committeeService.GetByAgmAsync(agmId, ct);
        var outgoing = positions.First(p => p.OfficeHolderTypeId is null);
        var incoming = activeMembers[9]; // not otherwise assigned an officeholder or committee role

        await _agmService.RecordSpecialElectionAsync(new RecordSpecialElectionRequest(
            OutgoingPositionRecordId: outgoing.Id,
            IncomingMemberId: incoming.Id,
            ReplacementDate: replacementDate), ct);
    }

    // -------------------------------------------------------------------------
    // Operating expenses
    // -------------------------------------------------------------------------

    private async Task SeedOperatingExpensesAsync(
        int year,
        Account bankAccount,
        Account hallHireExpense,
        Account costumesExpense,
        Account musicLicensingExpense,
        Account printingExpense,
        Account insuranceExpense,
        Account musicalDirectorExpense,
        Account bankFeesExpense,
        CancellationToken ct)
    {
        async Task PayAsync(DateTime date, decimal amount, Account account, string payee, string description)
        {
            if (date > SeedCurrentDate)
                return; // not paid yet

            await _expensePaymentService.RecordExpenseAsync(new RecordExpenseRequest
            {
                Date = date,
                Amount = amount,
                BankAccountId = bankAccount.Id,
                ExpenseAccountId = account.Id,
                Payee = payee,
                Description = description
            }, ct);
        }

        var termStarts = GetTermStartMondays(year);
        for (var i = 0; i < termStarts.Count; i++)
            await PayAsync(termStarts[i].AddDays(-3), 150m, hallHireExpense, "Community Hall Committee", $"Term {i + 1} hall hire {year}");

        await PayAsync(Utc(year, 2, 15), 120m, costumesExpense, "Clarence Valley Stage Supplies", $"Costume repairs {year}");
        await PayAsync(Utc(year, 8, 20), 350m, costumesExpense, "Clarence Valley Stage Supplies", $"New concert costumes {year}");

        await PayAsync(Utc(year, 2, 20), 180m, musicLicensingExpense, "APRA AMCOS", $"Term music licensing {year}");
        await PayAsync(Utc(year, 6, 10), 150m, musicLicensingExpense, "Sheet Music Plus", $"Eisteddfod sheet music {year}");

        await PayAsync(Utc(year, 7, 25), 90m, printingExpense, "Maclean Printing Co", $"Raffle ticket printing {year}");
        await PayAsync(Utc(year, 9, 5), 130m, printingExpense, "Maclean Printing Co", $"Concert programs & tickets {year}");

        foreach (var quarterEnd in new[] { Utc(year, 3, 31), Utc(year, 6, 30), Utc(year, 9, 30), Utc(year, 12, 31) })
            await PayAsync(quarterEnd, 15m, bankFeesExpense, "Regional Bank", "Quarterly account fee");

        await PayAsync(Utc(year, 10, 15), 1200m, insuranceExpense, "NFP Insurance Australia", $"{year} public liability & instrument insurance");
        await PayAsync(Utc(year, 10, 15), 500m, musicalDirectorExpense, "Musical Director", $"{year} annual musical director fee");
    }

    // -------------------------------------------------------------------------
    // Date helpers
    // -------------------------------------------------------------------------

    private static DateTime Utc(int year, int month, int day) => new(year, month, day, 0, 0, 0, DateTimeKind.Utc);

    /// <summary>Returns 40 Monday rehearsal dates per year (4 terms × 10 weeks).</summary>
    private static IEnumerable<DateTime> GetRehearsalDates(int year)
    {
        foreach (var termStart in GetTermStartMondays(year))
            for (var week = 0; week < 10; week++)
                yield return termStart.AddDays(week * 7);
    }

    /// <summary>
    /// First-Monday-of-term dates approximating the real NSW DoE school-term calendar
    /// (each term run out to exactly 10 weekly rehearsals, with 1.5–2 week term breaks and
    /// a ~6-week end-of-year break).
    /// </summary>
    private static IReadOnlyList<DateTime> GetTermStartMondays(int year) => year switch
    {
        2025 => [Utc(2025, 2, 3), Utc(2025, 4, 28), Utc(2025, 7, 21), Utc(2025, 10, 13)],
        2026 => [Utc(2026, 2, 2), Utc(2026, 4, 28), Utc(2026, 7, 21), Utc(2026, 10, 13)],
        _ => throw new ArgumentOutOfRangeException(nameof(year), $"No school terms configured for {year}.")
    };

    /// <summary>Eisteddfod Saturday in late July, within Term 3.</summary>
    private static DateTime GetEisteddfodDate(int year) => year switch
    {
        2025 => Utc(2025, 7, 26),
        2026 => Utc(2026, 7, 25),
        _ => throw new ArgumentOutOfRangeException(nameof(year), $"No Eisteddfod date configured for {year}.")
    };

    /// <summary>Consecutive Saturday (Maclean) / Sunday (Yamba) concert dates in September.</summary>
    private static (DateTime Saturday, DateTime Sunday) GetConcertDates(int year) => year switch
    {
        2025 => (Utc(2025, 9, 20), Utc(2025, 9, 21)),
        2026 => (Utc(2026, 9, 19), Utc(2026, 9, 20)),
        _ => throw new ArgumentOutOfRangeException(nameof(year), $"No concert dates configured for {year}.")
    };

    /// <summary>Raffle draw — third Saturday in August.</summary>
    private static DateTime GetRaffleDate(int year) => year switch
    {
        2025 => Utc(2025, 8, 16),
        2026 => Utc(2026, 8, 15),
        _ => throw new ArgumentOutOfRangeException(nameof(year), $"No raffle date configured for {year}.")
    };

    /// <summary>AGM on the last Monday in October — the day the new committee term commences.</summary>
    private static DateTime GetAgmDate(int year) => year switch
    {
        2025 => Utc(2025, 10, 27),
        2026 => Utc(2026, 10, 26),
        _ => throw new ArgumentOutOfRangeException(nameof(year), $"No AGM date configured for {year}.")
    };
}
