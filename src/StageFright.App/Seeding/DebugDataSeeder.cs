using Microsoft.Extensions.Logging;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Enums;
using StageFright.Core.Modules.Events;
using StageFright.Core.Modules.Finance;
using StageFright.Core.Modules.Members;
using StageFright.Core.Modules.Rehearsals;

namespace StageFright.App.Seeding;

/// <summary>
/// Seeds two full calendar years (2025–2026) of realistic small-NFP performing-group data
/// after the first-run wizard completes: 51 members (43 active/3 inactive/5 archived), a
/// petty-cash + single bank-account chart of accounts, 40 Monday-night rehearsals per year
/// during NSW school terms with probabilistic attendance, annual subscription fees, a July
/// Eisteddfod, a September Maclean/Yamba concert weekend, an annual raffle, committee
/// membership, an AGM, and a spread of dated operating expenses (insurance, musical
/// director, hall hire, costumes, licensing, printing, bank fees). Financial activity is
/// generated as if "today" were 1 July 2026 — nothing dated after that is posted, so
/// rehearsals/events in the second half of 2026 are scheduled but not yet paid/settled.
/// Only runs when the user opts in via the setup wizard checkbox.
/// </summary>
public class DebugDataSeeder : IDebugDataSeeder
{
    private const decimal PettyCashFloat = 50m;

    /// <summary>
    /// Seed data is generated as if "today" were 1 July 2026. Financial transactions
    /// (fees, payments, income, expenses) dated after this are not posted — they
    /// represent activity that has not happened yet from the seed data's point of view.
    /// </summary>
    private static readonly DateTime SeedCurrentDate = Utc(2026, 7, 1);

    private readonly IMemberService _memberService;
    private readonly IMemberRepository _memberRepository;
    private readonly IRehearsalService _rehearsalService;
    private readonly IAttendanceService _attendanceService;
    private readonly IEventTypeService _eventTypeService;
    private readonly IEventService _eventService;
    private readonly ICommitteeService _committeeService;
    private readonly IPaymentService _paymentService;
    private readonly IFeeRepository _feeRepository;
    private readonly IGLRepository _glRepository;
    private readonly IAccountService _accountService;
    private readonly IIncomeEntryService _incomeEntryService;
    private readonly IExpensePaymentService _expensePaymentService;
    private readonly IBankDepositService _bankDepositService;
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
        ICommitteeService committeeService,
        IPaymentService paymentService,
        IFeeRepository feeRepository,
        IGLRepository glRepository,
        IAccountService accountService,
        IIncomeEntryService incomeEntryService,
        IExpensePaymentService expensePaymentService,
        IBankDepositService bankDepositService,
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
        _committeeService = committeeService;
        _paymentService = paymentService;
        _feeRepository = feeRepository;
        _glRepository = glRepository;
        _accountService = accountService;
        _incomeEntryService = incomeEntryService;
        _expensePaymentService = expensePaymentService;
        _bankDepositService = bankDepositService;
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
        var agmType = eventTypes.First(et => et.Name == "Annual General Meeting");

        var settings = await _settingsService.GetAsync(ct)
            ?? throw new InvalidOperationException("Settings must be initialised before seeding debug data.");

        progress?.Report("Assigning committee positions…");
        await SeedCommitteeAsync(activeMembers, ct);

        var random = new Random(20250101); // fixed seed — deterministic across runs
        var attendanceProfile = BuildAttendanceProfile(activeMembers, random);

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
            await SeedAgmAsync(year, activeMembers, agmType, ct);

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
    // Members
    // -------------------------------------------------------------------------

    private async Task<(IReadOnlyList<Member> Active, IReadOnlyList<Member> Inactive, IReadOnlyList<Member> Archived)>
        CreateMembersAsync(CancellationToken ct)
    {
        // 51 members with realistic Clarence Valley NSW details. All joined before 2025 —
        // the first 43 are Active throughout 2025–2026, the next 3 are Inactive, and the
        // last 5 are Archived (soft-deleted); none change status during the seeded window.
        (string Name, string Address, string Phone, string Email, DateTime JoinDate)[] data =
        [
            ("Margaret Thompson",  "12 Clarence St, Maclean NSW 2463",        "02 6645 1234", "margaret.thompson@example.com",   new DateTime(2015, 3, 10)),
            ("Patricia Robinson",  "5 River Rd, Maclean NSW 2463",             "02 6645 2345", "patricia.robinson@example.com",   new DateTime(2016, 2,  1)),
            ("Susan Williams",     "88 Church St, Maclean NSW 2463",           "02 6645 3456", "susan.williams@example.com",      new DateTime(2014, 7, 15)),
            ("Barbara Taylor",     "3 Cameron Dr, Maclean NSW 2463",           "02 6645 4567", "barbara.taylor@example.com",      new DateTime(2017, 4, 22)),
            ("Dorothy Anderson",   "41 Elm Ave, Maclean NSW 2463",             "02 6645 5678", "dorothy.anderson@example.com",    new DateTime(2013, 9,  5)),
            ("Elizabeth Harris",   "7 Pacific Hwy, Maclean NSW 2463",          "02 6645 6789", "elizabeth.harris@example.com",    new DateTime(2018, 1, 30)),
            ("Carol Martin",       "22 Oak St, Lawrence NSW 2460",             "02 6647 7890", "carol.martin@example.com",        new DateTime(2015, 6, 14)),
            ("Helen Wilson",       "9 Palmers Island Rd, Lawrence NSW 2460",   "02 6647 8901", "helen.wilson@example.com",        new DateTime(2016, 11, 3)),
            ("Catherine Moore",    "66 Station St, Grafton NSW 2460",          "02 6642 9012", "catherine.moore@example.com",     new DateTime(2019, 2, 28)),
            ("Frances Davis",      "14 Crown St, Grafton NSW 2460",            "02 6642 0123", "frances.davis@example.com",       new DateTime(2014, 8, 19)),
            ("Linda Johnson",      "33 Villiers St, Grafton NSW 2460",         "02 6642 1234", "linda.johnson@example.com",       new DateTime(2017, 5,  7)),
            ("Ruth Mitchell",      "5 Newman St, Yamba NSW 2464",              "02 6646 2345", "ruth.mitchell@example.com",       new DateTime(2020, 1, 15)),
            ("Joan Lewis",         "18 Yamba Rd, Yamba NSW 2464",              "02 6646 3456", "joan.lewis@example.com",          new DateTime(2015, 10, 22)),
            ("Shirley Walker",     "42 Coldstream St, Yamba NSW 2464",         "02 6646 4567", "shirley.walker@example.com",      new DateTime(2016, 3,  8)),
            ("Anne Campbell",      "91 River St, Yamba NSW 2464",              "02 6646 5678", "anne.campbell@example.com",       new DateTime(2018, 7, 14)),
            ("Michelle Lee",       "28 Flinders St, Iluka NSW 2454",           "02 6646 6789", "michelle.lee@example.com",        new DateTime(2021, 2,  1)),
            ("Amanda Scott",       "15 Charles St, Iluka NSW 2454",            "02 6646 7890", "amanda.scott@example.com",        new DateTime(2019, 9, 30)),
            ("Stephanie Hall",     "7 Solitary Is Way, Wooli NSW 2462",        "02 6649 8901", "stephanie.hall@example.com",      new DateTime(2022, 1, 10)),
            ("Rachel Young",       "3 Orara St, Grafton NSW 2460",             "02 6642 9012", "rachel.young@example.com",        new DateTime(2020, 6,  5)),
            ("Karen Roberts",      "55 Pound St, Grafton NSW 2460",            "02 6642 0234", "karen.roberts@example.com",       new DateTime(2017, 12, 1)),
            ("Robert Smith",       "10 Main St, Maclean NSW 2463",             "02 6645 1111", "robert.smith@example.com",        new DateTime(2015, 1, 20)),
            ("John Davies",        "44 Park Ave, Maclean NSW 2463",            "02 6645 2222", "john.davies@example.com",         new DateTime(2016, 4, 12)),
            ("William Evans",      "6 Queen St, Maclean NSW 2463",             "02 6645 3333", "william.evans@example.com",       new DateTime(2013, 11, 28)),
            ("David Clark",        "19 Wharf St, Maclean NSW 2463",            "02 6645 4444", "david.clark@example.com",         new DateTime(2018, 8,  3)),
            ("James Hughes",       "31 Bruce Hwy, Lawrence NSW 2460",          "02 6647 5555", "james.hughes@example.com",        new DateTime(2014, 5, 17)),
            ("Michael Turner",     "8 Cooper Rd, Lawrence NSW 2460",           "02 6647 6666", "michael.turner@example.com",      new DateTime(2017, 3, 25)),
            ("Thomas Baker",       "52 Tinonee Rd, Maclean NSW 2463",          "02 6645 7777", "thomas.baker@example.com",        new DateTime(2016, 7,  9)),
            ("Christopher Morris", "17 Orara Way, Grafton NSW 2460",           "02 6642 8888", "christopher.morris@example.com",  new DateTime(2019, 11, 16)),
            ("Andrew Price",       "4 Fitzroy St, Grafton NSW 2460",           "02 6642 9999", "andrew.price@example.com",        new DateTime(2021, 4,  1)),
            ("Kenneth Bennett",    "28 Mary St, Grafton NSW 2460",             "02 6642 0001", "kenneth.bennett@example.com",     new DateTime(2015, 9, 14)),
            ("Steven Cook",        "73 Duke St, Grafton NSW 2460",             "02 6642 1112", "steven.cook@example.com",         new DateTime(2018, 2, 20)),
            ("Gregory Collins",    "11 Yamba Rd, Yamba NSW 2464",              "02 6646 2223", "gregory.collins@example.com",     new DateTime(2014, 12, 6)),
            ("Paul Ward",          "36 Coldstream St, Yamba NSW 2464",         "02 6646 3334", "paul.ward@example.com",           new DateTime(2017, 6, 18)),
            ("Brian James",        "22 The Esplanade, Yamba NSW 2464",         "02 6646 4445", "brian.james@example.com",         new DateTime(2020, 3, 11)),
            ("Kevin Phillips",     "5 Carrs Dr, Yamba NSW 2464",               "02 6646 5556", "kevin.phillips@example.com",      new DateTime(2016, 10, 24)),
            ("Edward Stewart",     "14 Wooli St, Wooli NSW 2462",              "02 6649 6667", "edward.stewart@example.com",      new DateTime(2019, 7,  8)),
            ("Ronald Thomson",     "8 Pacific Pde, Wooli NSW 2462",            "02 6649 7778", "ronald.thomson@example.com",      new DateTime(2022, 5, 15)),
            ("Daniel White",       "29 Sandy Beach Rd, Wooli NSW 2462",        "02 6649 8889", "daniel.white@example.com",        new DateTime(2013, 8, 27)),
            ("Mark Green",         "17 Palmers Island Rd, Grafton NSW 2460",   "02 6642 9990", "mark.green@example.com",          new DateTime(2021, 1,  4)),
            ("Timothy Hill",       "6 South Arm Rd, Brushgrove NSW 2460",      "02 6647 0001", "timothy.hill@example.com",        new DateTime(2018, 4, 16)),
            // Additional Active members (41–43)
            ("Emma Nguyen",        "21 Yamba Rd, Yamba NSW 2464",              "02 6646 1123", "emma.nguyen@example.com",         new DateTime(2016, 5, 12)),
            ("Liam Fitzgerald",    "9 Prince St, Grafton NSW 2460",            "02 6642 2234", "liam.fitzgerald@example.com",     new DateTime(2018, 9,  3)),
            ("Sophie Chen",        "14 River St, Maclean NSW 2463",           "02 6645 3345", "sophie.chen@example.com",         new DateTime(2019, 11, 20)),
            // Inactive members (44–46)
            ("Harold Bishop",      "6 Ferry St, Ulmarra NSW 2462",             "02 6644 4456", "harold.bishop@example.com",       new DateTime(2013, 2, 14)),
            ("Nancy Fraser",       "3 Coldstream St, Yamba NSW 2464",          "02 6646 5567", "nancy.fraser@example.com",        new DateTime(2014, 6,  8)),
            ("Victor Osborne",     "18 Bent St, Chatsworth Island NSW 2469",   "02 6645 6678", "victor.osborne@example.com",      new DateTime(2012, 10, 30)),
            // Archived members (47–51)
            ("Beatrice Sinclair",  "27 Yamba Rd, Yamba NSW 2464",              "02 6646 7789", "beatrice.sinclair@example.com",   new DateTime(2011, 3, 17)),
            ("Gerald Whitfield",   "5 Spenser St, Grafton NSW 2460",           "02 6642 8890", "gerald.whitfield@example.com",    new DateTime(2012, 7, 22)),
            ("Ivy Sorenson",       "12 Ashby Island Rd, Ashby NSW 2463",       "02 6645 9901", "ivy.sorenson@example.com",        new DateTime(2013, 1,  9)),
            ("Norman Blackwood",   "44 Pound St, Grafton NSW 2460",            "02 6642 0012", "norman.blackwood@example.com",    new DateTime(2010, 12, 4)),
            ("Agnes Pemberton",    "8 Townsend Rd, Townsend NSW 2463",         "02 6645 1129", "agnes.pemberton@example.com",     new DateTime(2014, 4, 27)),
        ];

        var all = new List<Member>(data.Length);
        foreach (var (name, address, phone, email, joinDate) in data)
        {
            var member = await _memberService.CreateAsync(new CreateMemberRequest
            {
                Name = name,
                StreetAddress = address,
                Phone = phone,
                Email = email,
                JoinDate = joinDate
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
    // Committee
    // -------------------------------------------------------------------------

    /// <summary>
    /// The committee term runs 1 year from the last Monday in October; the same nine
    /// members hold the same positions across both the 2025 and 2026 calendar years.
    /// </summary>
    private async Task SeedCommitteeAsync(IReadOnlyList<Member> activeMembers, CancellationToken ct)
    {
        string[] positions =
        [
            "President", "Secretary", "Treasurer", "Welfare Officer",
            "Committee Member", "Committee Member", "Committee Member", "Committee Member", "Committee Member"
        ];

        for (var i = 0; i < positions.Length; i++)
        {
            var member = activeMembers[i];
            await _committeeService.AddOrUpdateAsync(member.Id, 2025, positions[i], ct);
            await _committeeService.AddOrUpdateAsync(member.Id, 2026, positions[i], ct);
        }
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
        var gstCode = settings.IsGstRegistered ? (settings.AnnualFeeGstCode ?? GstCode.GstFree) : (GstCode?)null;
        var (incomeAmount, gstAmount) = gstCode == GstCode.Gst
            ? GstCalculator.SplitInclusive(settings.AnnualFee)
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
                GstCode = gstCode,
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
                    MemberId = memberId, FeeId = savedFee.Id, GstCode = gstCode,
                    Description = $"Annual membership fee {year}", CreatedAt = now
                },
                new()
                {
                    Id = Guid.NewGuid(), Date = feeDate,
                    AccountId = incomeAccount.Id, GLAccount = incomeAccount.AccountNumber,
                    DebitAmount = 0m, CreditAmount = incomeAmount,
                    FeeId = savedFee.Id, GstCode = gstCode,
                    Description = $"Annual membership fee income {year}", CreatedAt = now
                }
            };

            if (gstAmount != 0m)
            {
                lines.Add(new Transaction
                {
                    Id = Guid.NewGuid(), Date = feeDate,
                    AccountId = SystemAccounts.GstCollectedId, GLAccount = SystemAccounts.GstCollectedNumber,
                    DebitAmount = 0m, CreditAmount = gstAmount,
                    FeeId = savedFee.Id, GstCode = gstCode,
                    Description = $"GST collected — annual membership fee {year}", CreatedAt = now
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
    /// One rehearsal per Monday across 40 term weeks. Attendance fees are collected at the
    /// door into petty cash and, 2–3 days later, swept to the bank account above a small
    /// float retained in the tin.
    /// </summary>
    private async Task SeedRehearsalsAsync(
        int year,
        IReadOnlyList<Member> activeMembers,
        IReadOnlyDictionary<Guid, double> attendanceProfile,
        Account bankAccount,
        Random random,
        CancellationToken ct)
    {
        foreach (var date in GetRehearsalDates(year))
        {
            var rehearsal = await _rehearsalService.ScheduleAsync(new ScheduleRehearsalRequest
            {
                Date = date,
                Time = new TimeSpan(19, 30, 0) // 7:30 PM
            }, ct);

            if (date > SeedCurrentDate)
                continue; // future rehearsal — attendance not yet taken, no fees collected

            var items = activeMembers
                .Select(m => new AttendanceBatchItem
                {
                    MemberId = m.Id,
                    Attended = random.NextDouble() < attendanceProfile[m.Id],
                    MarkAsUnpaid = false // attendance fee collected at the door
                })
                .ToList();

            await _attendanceService.RecordBatchAsync(rehearsal.Id, items, ct);

            var sweepDate = date.AddDays(random.Next(2, 4)); // 2 or 3 days later
            var pettyCashBalance = await _glRepository.GetAccountBalanceAsync(SystemAccounts.CashId, sweepDate, ct);
            if (pettyCashBalance > PettyCashFloat)
            {
                await _bankDepositService.RecordDepositAsync(new RecordBankDepositRequest
                {
                    Date = sweepDate,
                    Amount = Math.Round(pettyCashBalance - PettyCashFloat, 2),
                    ToAccountId = bankAccount.Id,
                    Description = "Banking of rehearsal attendance fees"
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

    private async Task SeedAgmAsync(int year, IReadOnlyList<Member> activeMembers, EventType agmType, CancellationToken ct)
    {
        var agmDate = GetAgmDate(year);
        var evt = await _eventService.ScheduleAsync(new ScheduleEventRequest
        {
            Date = agmDate,
            EventTypeId = agmType.Id,
            Notes = $"{year} Annual General Meeting — new committee term commences"
        }, ct);

        if (agmDate <= SeedCurrentDate)
        {
            var items = activeMembers.Select(m => new ParticipationBatchItem { MemberId = m.Id, Participated = true }).ToList();
            await _eventService.RecordParticipationAsync(evt.Id, items, ct);
        }
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
