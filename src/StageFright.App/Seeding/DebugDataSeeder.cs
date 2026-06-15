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
/// Seeds 3 years (2024–2026) of realistic test data after the first-run wizard completes.
/// 40 members, 40 NSW-school-term rehearsals/year, annual Eisteddfod, two annual concerts.
/// Registered and invoked only in DEBUG builds.
/// </summary>
public class DebugDataSeeder : IDebugDataSeeder
{
    // Well-known system category GUIDs seeded by StageFrightDbContext
    private static readonly Guid CashCategoryId = new("00000000-0000-0000-0000-000000000001");
    private static readonly Guid MemberReceivableCategoryId = new("00000000-0000-0000-0000-000000000002");

    // Seed reference date — keeps generated data stable regardless of when seeding runs
    private static readonly DateTime SeedToday = new(2026, 6, 15, 0, 0, 0, DateTimeKind.Utc);

    private readonly IMemberService _memberService;
    private readonly IRehearsalService _rehearsalService;
    private readonly IAttendanceService _attendanceService;
    private readonly IEventTypeService _eventTypeService;
    private readonly IEventService _eventService;
    private readonly IFeeService _feeService;
    private readonly IPaymentService _paymentService;
    private readonly IFeeRepository _feeRepository;
    private readonly IPaymentRepository _paymentRepository;
    private readonly IGLRepository _glRepository;
    private readonly ICategoryService _categoryService;
    private readonly ISettingsService _settingsService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<DebugDataSeeder> _logger;

    public DebugDataSeeder(
        IMemberService memberService,
        IRehearsalService rehearsalService,
        IAttendanceService attendanceService,
        IEventTypeService eventTypeService,
        IEventService eventService,
        IFeeService feeService,
        IPaymentService paymentService,
        IFeeRepository feeRepository,
        IPaymentRepository paymentRepository,
        IGLRepository glRepository,
        ICategoryService categoryService,
        ISettingsService settingsService,
        IUnitOfWork unitOfWork,
        ILogger<DebugDataSeeder> logger)
    {
        _memberService = memberService;
        _rehearsalService = rehearsalService;
        _attendanceService = attendanceService;
        _eventTypeService = eventTypeService;
        _eventService = eventService;
        _feeService = feeService;
        _paymentService = paymentService;
        _feeRepository = feeRepository;
        _paymentRepository = paymentRepository;
        _glRepository = glRepository;
        _categoryService = categoryService;
        _settingsService = settingsService;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task SeedAsync(CancellationToken ct = default)
    {
        var existing = await _memberService.GetByStatusAsync(MemberStatus.Active, ct);
        if (existing.Count > 0)
        {
            _logger.LogInformation("Debug seed data already present ({Count} members); skipping.", existing.Count);
            return;
        }

        _logger.LogInformation("Seeding debug data...");

        // Income categories — must exist before attendance/fee GL entries are written
        var membershipFeeCategory = await _categoryService.CreateAsync("Membership Fees", CategoryType.Income, ct);
        var concertIncomeCategory = await _categoryService.CreateAsync("Concert Income", CategoryType.Income, ct);

        var members = await CreateMembersAsync(ct);
        // First 32 of 40 members (80%) attend every rehearsal
        var regularAttendeeIds = new HashSet<Guid>(members.Take(32).Select(m => m.Id));

        var eventTypes = await _eventTypeService.GetAllAsync(ct);
        var eisteddfodType = eventTypes.First(et => et.Name == "Eisteddfod");
        var performanceType = eventTypes.First(et => et.Name == "Performance");

        var settings = await _settingsService.GetAsync(ct)
            ?? throw new InvalidOperationException("Settings must be initialised before seeding debug data.");

        foreach (var year in new[] { 2024, 2025, 2026 })
        {
            _logger.LogInformation("Seeding {Year}...", year);
            await SeedAnnualFeesAsync(year, members, membershipFeeCategory, settings.AnnualFee, ct);
            await SeedRehearsalsAsync(year, members, regularAttendeeIds, ct);
            await SeedEisteddfodAsync(year, members, eisteddfodType, ct);
            if (year < 2026)
                await SeedConcertsAsync(year, members, performanceType, concertIncomeCategory, ct);
        }

        _logger.LogInformation("Debug data seed complete — {Count} members, 3 years.", members.Count);
    }

    // -------------------------------------------------------------------------
    // Members
    // -------------------------------------------------------------------------

    private async Task<IReadOnlyList<Member>> CreateMembersAsync(CancellationToken ct)
    {
        // 40 members with realistic Clarence Valley NSW details
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
        ];

        var result = new List<Member>(data.Length);
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
            result.Add(member);
        }
        return result;
    }

    // -------------------------------------------------------------------------
    // Annual fees
    // -------------------------------------------------------------------------

    private async Task SeedAnnualFeesAsync(
        int year,
        IReadOnlyList<Member> members,
        Category incomeCategory,
        decimal annualFeeAmount,
        CancellationToken ct)
    {
        if (annualFeeAmount <= 0m) return;

        if (year == DateTime.UtcNow.Year)
        {
            // Current year: use the standard service pipeline so business rules are respected
            await _feeService.ApplyAnnualFeesAsync(members.Select(m => m.Id).ToList(), ct);

            var paymentDate = new DateTime(year, 2, 1, 0, 0, 0, DateTimeKind.Utc);
            foreach (var member in members)
            {
                await _paymentService.RecordAsync(new RecordPaymentRequest
                {
                    MemberId = member.Id,
                    Date = paymentDate,
                    Amount = annualFeeAmount,
                    PaymentMethod = PaymentMethod.Cash,
                    PaymentType = PaymentType.Annual,
                    Notes = $"{year} annual membership fee"
                }, ct);
            }
            return;
        }

        // Historical years: replicate the FeeService + PaymentService GL pattern with historical dates.
        // PaidAtCreation=true so these fees are excluded from GetUnpaidOrderedFifoAsync queries.
        var feeDate = new DateTime(year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var dueDate = new DateTime(year, 12, 31, 0, 0, 0, DateTimeKind.Utc);
        var payDate = new DateTime(year, 2, 1, 0, 0, 0, DateTimeKind.Utc);

        foreach (var member in members)
        {
            await _unitOfWork.ExecuteInTransactionAsync(async innerCt =>
            {
                var now = DateTime.UtcNow;

                // Fee record
                var fee = new Fee
                {
                    Id = Guid.NewGuid(),
                    MemberId = member.Id,
                    FeeType = FeeType.Annual,
                    Amount = annualFeeAmount,
                    FeeDate = feeDate,
                    DueDate = dueDate,
                    PaidAtCreation = true,
                    CreatedAt = now
                };
                var savedFee = await _feeRepository.AddAsync(fee, innerCt);

                // GL accrual: Debit MemberReceivable / Credit Income
                await _glRepository.AddPairAsync(
                    new Transaction
                    {
                        Id = Guid.NewGuid(), Date = feeDate,
                        CategoryId = MemberReceivableCategoryId, GLAccount = "0101",
                        DebitAmount = annualFeeAmount, CreditAmount = 0m,
                        MemberId = member.Id, FeeId = savedFee.Id,
                        Description = $"Annual membership fee {year}",
                        CreatedAt = now
                    },
                    new Transaction
                    {
                        Id = Guid.NewGuid(), Date = feeDate,
                        CategoryId = incomeCategory.Id, GLAccount = incomeCategory.GLAccount,
                        DebitAmount = 0m, CreditAmount = annualFeeAmount,
                        FeeId = savedFee.Id,
                        Description = $"Annual membership fee income {year}",
                        CreatedAt = now
                    },
                    innerCt);

                // Payment record
                var payment = new Payment
                {
                    Id = Guid.NewGuid(),
                    MemberId = member.Id,
                    Date = payDate,
                    Amount = annualFeeAmount,
                    PaymentMethod = PaymentMethod.Cash,
                    PaymentType = PaymentType.Annual,
                    Notes = $"{year} annual membership fee",
                    CreatedAt = now,
                    UpdatedAt = now
                };
                var savedPayment = await _paymentRepository.AddAsync(payment, innerCt);

                // GL payment clearing: Debit Cash / Credit MemberReceivable
                await _glRepository.AddPairAsync(
                    new Transaction
                    {
                        Id = Guid.NewGuid(), Date = payDate,
                        CategoryId = CashCategoryId, GLAccount = "0100",
                        DebitAmount = annualFeeAmount, CreditAmount = 0m,
                        MemberId = member.Id, PaymentId = savedPayment.Id, FeeId = savedFee.Id,
                        Description = $"Annual fee cash receipt {year}",
                        CreatedAt = now
                    },
                    new Transaction
                    {
                        Id = Guid.NewGuid(), Date = payDate,
                        CategoryId = MemberReceivableCategoryId, GLAccount = "0101",
                        DebitAmount = 0m, CreditAmount = annualFeeAmount,
                        MemberId = member.Id, PaymentId = savedPayment.Id, FeeId = savedFee.Id,
                        Description = $"Annual fee receivable cleared {year}",
                        CreatedAt = now
                    },
                    innerCt);
            }, ct);
        }
    }

    // -------------------------------------------------------------------------
    // Rehearsals and attendance
    // -------------------------------------------------------------------------

    private async Task SeedRehearsalsAsync(
        int year,
        IReadOnlyList<Member> members,
        HashSet<Guid> regularAttendeeIds,
        CancellationToken ct)
    {
        foreach (var date in GetRehearsalDates(year))
        {
            var rehearsal = await _rehearsalService.ScheduleAsync(new ScheduleRehearsalRequest
            {
                Date = date,
                Time = new TimeSpan(19, 30, 0) // 7:30 PM
            }, ct);

            var items = members
                .Select(m => new AttendanceBatchItem
                {
                    MemberId = m.Id,
                    Attended = regularAttendeeIds.Contains(m.Id),
                    MarkAsUnpaid = false // attendance fee collected at the door
                })
                .ToList();

            await _attendanceService.RecordBatchAsync(rehearsal.Id, items, ct);
        }
    }

    // -------------------------------------------------------------------------
    // Eisteddfod event
    // -------------------------------------------------------------------------

    private async Task SeedEisteddfodAsync(
        int year,
        IReadOnlyList<Member> members,
        EventType eisteddfodType,
        CancellationToken ct)
    {
        var eventDate = GetEisteddfodDate(year);
        if (eventDate >= SeedToday) return;

        var evt = await _eventService.ScheduleAsync(new ScheduleEventRequest
        {
            Date = eventDate,
            EventTypeId = eisteddfodType.Id,
            Notes = $"{year} Eisteddfod — Sunday, 2 hours"
        }, ct);

        // All members participate in the Eisteddfod
        var items = members
            .Select(m => new ParticipationBatchItem { MemberId = m.Id, Participated = true })
            .ToList();

        await _eventService.RecordParticipationAsync(evt.Id, items, ct);
    }

    // -------------------------------------------------------------------------
    // Annual concerts
    // -------------------------------------------------------------------------

    private async Task SeedConcertsAsync(
        int year,
        IReadOnlyList<Member> members,
        EventType performanceType,
        Category concertIncomeCategory,
        CancellationToken ct)
    {
        var (satDate, sunDate) = GetConcertDates(year);

        var participationItems = members
            .Select(m => new ParticipationBatchItem { MemberId = m.Id, Participated = true })
            .ToList();

        // Saturday — Maclean
        var satEvent = await _eventService.ScheduleAsync(new ScheduleEventRequest
        {
            Date = satDate,
            EventTypeId = performanceType.Id,
            Notes = $"{year} Annual Concert — Saturday, Maclean"
        }, ct);
        await _eventService.RecordParticipationAsync(satEvent.Id, participationItems, ct);
        await RecordConcertIncomeAsync(satDate, 800m, $"{year} Maclean Concert income", concertIncomeCategory, ct);

        // Sunday — Yamba
        var sunEvent = await _eventService.ScheduleAsync(new ScheduleEventRequest
        {
            Date = sunDate,
            EventTypeId = performanceType.Id,
            Notes = $"{year} Annual Concert — Sunday, Yamba"
        }, ct);
        await _eventService.RecordParticipationAsync(sunEvent.Id, participationItems, ct);
        await RecordConcertIncomeAsync(sunDate, 800m, $"{year} Yamba Concert income", concertIncomeCategory, ct);
    }

    private async Task RecordConcertIncomeAsync(
        DateTime date,
        decimal amount,
        string description,
        Category incomeCategory,
        CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        await _glRepository.AddPairAsync(
            new Transaction
            {
                Id = Guid.NewGuid(), Date = date,
                CategoryId = CashCategoryId, GLAccount = "0100",
                DebitAmount = amount, CreditAmount = 0m,
                Description = description, CreatedAt = now
            },
            new Transaction
            {
                Id = Guid.NewGuid(), Date = date,
                CategoryId = incomeCategory.Id, GLAccount = incomeCategory.GLAccount,
                DebitAmount = 0m, CreditAmount = amount,
                Description = description, CreatedAt = now
            },
            ct);
    }

    // -------------------------------------------------------------------------
    // Date helpers
    // -------------------------------------------------------------------------

    /// <summary>Returns one Tuesday per week during NSW school terms, up to SeedToday.</summary>
    private static IEnumerable<DateTime> GetRehearsalDates(int year)
    {
        foreach (var (start, end) in GetSchoolTerms(year))
        {
            // Advance to the first Tuesday on or after term start
            var day = start;
            while (day.DayOfWeek != DayOfWeek.Tuesday)
                day = day.AddDays(1);

            while (day <= end && day < SeedToday)
            {
                yield return new DateTime(day.Year, day.Month, day.Day, 0, 0, 0, DateTimeKind.Utc);
                day = day.AddDays(7);
            }
        }
    }

    /// <summary>NSW public school term date ranges, sourced from the official NSW DoE calendar.</summary>
    private static IReadOnlyList<(DateTime Start, DateTime End)> GetSchoolTerms(int year) => year switch
    {
        2024 =>
        [
            (new(2024,  1, 29, 0, 0, 0, DateTimeKind.Utc), new(2024,  4,  5, 0, 0, 0, DateTimeKind.Utc)),
            (new(2024,  4, 22, 0, 0, 0, DateTimeKind.Utc), new(2024,  6, 28, 0, 0, 0, DateTimeKind.Utc)),
            (new(2024,  7, 22, 0, 0, 0, DateTimeKind.Utc), new(2024,  9, 27, 0, 0, 0, DateTimeKind.Utc)),
            (new(2024, 10, 14, 0, 0, 0, DateTimeKind.Utc), new(2024, 12, 20, 0, 0, 0, DateTimeKind.Utc)),
        ],
        2025 =>
        [
            (new(2025,  2,  3, 0, 0, 0, DateTimeKind.Utc), new(2025,  4, 11, 0, 0, 0, DateTimeKind.Utc)),
            (new(2025,  4, 28, 0, 0, 0, DateTimeKind.Utc), new(2025,  7,  4, 0, 0, 0, DateTimeKind.Utc)),
            (new(2025,  7, 21, 0, 0, 0, DateTimeKind.Utc), new(2025,  9, 26, 0, 0, 0, DateTimeKind.Utc)),
            (new(2025, 10, 13, 0, 0, 0, DateTimeKind.Utc), new(2025, 12, 19, 0, 0, 0, DateTimeKind.Utc)),
        ],
        2026 =>
        [
            (new(2026,  1, 28, 0, 0, 0, DateTimeKind.Utc), new(2026,  4,  9, 0, 0, 0, DateTimeKind.Utc)),
            (new(2026,  4, 27, 0, 0, 0, DateTimeKind.Utc), new(2026,  6, 26, 0, 0, 0, DateTimeKind.Utc)),
        ],
        _ => throw new ArgumentOutOfRangeException(nameof(year), $"No school terms configured for {year}.")
    };

    /// <summary>Eisteddfod Sunday in June; verified against actual calendar.</summary>
    private static DateTime GetEisteddfodDate(int year) => year switch
    {
        // 2024 Jun 23 = Sunday (Term 2 ends Jun 28)
        2024 => new DateTime(2024, 6, 23, 0, 0, 0, DateTimeKind.Utc),
        // 2025 Jun 22 = Sunday (Term 2 ends Jul 4)
        2025 => new DateTime(2025, 6, 22, 0, 0, 0, DateTimeKind.Utc),
        // 2026 Jun 14 = Sunday (day before SeedToday)
        2026 => new DateTime(2026, 6, 14, 0, 0, 0, DateTimeKind.Utc),
        _ => throw new ArgumentOutOfRangeException(nameof(year), $"No Eisteddfod date configured for {year}.")
    };

    /// <summary>Consecutive Saturday/Sunday concert dates in November (Term 4).</summary>
    private static (DateTime Saturday, DateTime Sunday) GetConcertDates(int year) => year switch
    {
        // Nov 16 = Saturday, Nov 17 = Sunday  (Nov 1, 2024 = Friday)
        2024 => (new DateTime(2024, 11, 16, 0, 0, 0, DateTimeKind.Utc), new DateTime(2024, 11, 17, 0, 0, 0, DateTimeKind.Utc)),
        // Nov 15 = Saturday, Nov 16 = Sunday  (Nov 1, 2025 = Saturday)
        2025 => (new DateTime(2025, 11, 15, 0, 0, 0, DateTimeKind.Utc), new DateTime(2025, 11, 16, 0, 0, 0, DateTimeKind.Utc)),
        _ => throw new ArgumentOutOfRangeException(nameof(year), $"No concert dates configured for {year}.")
    };
}
