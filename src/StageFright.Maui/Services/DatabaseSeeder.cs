using StageFright.Core.Entities;
using StageFright.Data.Context;

namespace StageFright.Maui.Services;

/// <summary>
/// Implementation of IDatabaseSeeder that generates comprehensive test data.
/// Creates 35 active members + 5 inactive members, 4 years of rehearsals/events,
/// attendance records (80% attendance), and financial records.
/// </summary>
public class DatabaseSeeder : IDatabaseSeeder
{
	private readonly Random _random = new(42); // Fixed seed for reproducibility
	private DateTime _now = new(2026, 5, 20); // May 20, 2026

	// Sample names for test data
	private readonly string[] FirstNames = new[]
	{
		"Alasdair", "Braden", "Catriona", "Duncan", "Eileen", "Fiona", "Graeme", "Hannah", "Ian", "Janet",
		"Kenneth", "Lachlan", "Moira", "Neil", "Olivia", "Pádraig", "Quinton", "Robertson", "Sheena", "Thomas",
		"Urchin", "Vanessa", "William", "Xavier", "Yasmine", "Zoe", "Angus", "Beatrice", "Colin", "Dougal",
		"Esme", "Finlay", "Gideon", "Helen", "Iain", "Joanna", "Keira", "Lorne", "Malcolm", "Nessa"
	};

	private readonly string[] LastNames = new[]
	{
		"MacLeod", "MacDonald", "MacKenzie", "MacKay", "MacLaren", "MacFarlane", "MacGregor", "MacLachlan",
		"MacPherson", "MacRae", "McCallum", "McAlpine", "McIndoe", "McIntyre", "McEwen", "McGill",
		"Morrison", "Munro", "Murray", "Campbell", "Buchanan", "Cameron", "Chisholm", "Cochrane",
		"Colquhoun", "Comyn", "Cummings", "Dalrymple", "Dewar", "Douglas", "Drummond", "Dunbar",
		"Duncan", "Dunn", "Elliot", "Erskine", "Falconer", "Fergusson", "Findlay", "Fleming"
	};

	private readonly string[] Streets = new[]
	{
		"Main Street", "Oak Avenue", "Elm Street", "Maple Drive", "Pine Road", "Cedar Lane",
		"Birch Boulevard", "Willow Way", "Ash Avenue", "Poplar Place", "Sycamore Street",
		"Hazel Hill", "Rowan Road", "Larch Lane", "Walnut Way"
	};

	public async Task SeedDatabaseAsync(StageFrightContext context)
	{
		// Only seed if database is empty
		if (context.Members.Any())
		{
			return;
		}

		// Create settings
		var settings = new Settings
		{
			Id = Guid.NewGuid(),
			OrganizationName = "StageFright Community Singers",
			AnnualFee = 150m,
			AttendanceFee = 5m,
			RenewalMonth = 1,
			CommitteeRenewalMonth = 1,
			LastCommitteeResetYear = 2026,
			MaxAgeRange = 150,
			MinimumMemberAge = 0,
			Theme = "Dark",
			CreatedAt = _now,
			ModifiedAt = _now
		};
		context.Settings.Add(settings);

		// Create members
		var activeMembers = new List<Member>();
		var inactiveMembers = new List<Member>();

		// Generate 35 active members
		for (int i = 0; i < 35; i++)
		{
			var member = new Member
			{
				Id = Guid.NewGuid(),
				Name = $"{FirstNames[i % FirstNames.Length]} {LastNames[(i * 7) % LastNames.Length]}",
				StreetAddress = $"{_random.Next(1, 999)} {Streets[_random.Next(Streets.Length)]}",
				Phone = $"0{_random.Next(100, 999)} {_random.Next(1000000, 9999999)}",
				Email = $"member{i + 1}@stagefright.local",
				JoinDate = new DateTime(_random.Next(2020, 2025), _random.Next(1, 13), _random.Next(1, 29)),
				DateOfBirth = new DateTime(_random.Next(1960, 1995), _random.Next(1, 13), _random.Next(1, 29)),
				Status = "Active",
				ActivateDate = null,
				InactivateDate = null,
				IsDeleted = false
			};
			activeMembers.Add(member);
			context.Members.Add(member);
		}

		// Generate 5 inactive members
		for (int i = 0; i < 5; i++)
		{
			var member = new Member
			{
				Id = Guid.NewGuid(),
				Name = $"{FirstNames[(35 + i) % FirstNames.Length]} {LastNames[((35 + i) * 7) % LastNames.Length]}",
				StreetAddress = $"{_random.Next(1, 999)} {Streets[_random.Next(Streets.Length)]}",
				Phone = $"0{_random.Next(100, 999)} {_random.Next(1000000, 9999999)}",
				Email = $"inactive{i + 1}@stagefright.local",
				JoinDate = new DateTime(_random.Next(2020, 2023), _random.Next(1, 13), _random.Next(1, 29)),
				DateOfBirth = new DateTime(_random.Next(1960, 1990), _random.Next(1, 13), _random.Next(1, 29)),
				Status = "Inactive",
				ActivateDate = new DateTime(_random.Next(2020, 2023), _random.Next(1, 13), _random.Next(1, 29)),
				InactivateDate = new DateTime(_random.Next(2023, 2025), _random.Next(1, 13), _random.Next(1, 29)),
				IsDeleted = false
			};
			inactiveMembers.Add(member);
			context.Members.Add(member);
		}

		await context.SaveChangesAsync();

		// Create rehearsals and events for 4 years (2023-2026)
		var allRehearsals = new List<Rehearsal>();
		var allEvents = new List<Event>();

		for (int year = 2023; year <= 2026; year++)
		{
			// Create 40 rehearsals for this year
			for (int i = 0; i < 40; i++)
			{
				var rehearsalDate = new DateTime(year, 1, 1).AddDays(i * 9); // Roughly weekly

				// Adjust 2026 rehearsals to have exactly 18 in the future
				if (year == 2026)
				{
					// Calculate dates so we have 22 past and 18 future
					rehearsalDate = new DateTime(2026, 1, 1).AddDays(i * 9);
					if (i < 22)
					{
						// First 22 are in the past (distributed across Jan-April)
						rehearsalDate = new DateTime(2026, 1, 1).AddDays(i * 11);
					}
					else
					{
						// Next 18 are in the future (starting from late May onwards)
						rehearsalDate = _now.AddDays((i - 22 + 1) * 14);
					}
				}

				var rehearsal = new Rehearsal
				{
					Id = Guid.NewGuid(),
					Date = rehearsalDate,
					Time = new TimeSpan(19, 30, 0), // 7:30 PM
					Notes = $"Rehearsal {i + 1}",
					IsDeleted = false
				};
				allRehearsals.Add(rehearsal);
				context.Rehearsals.Add(rehearsal);
			}

			// Create events for this year
			// 1. Eisteddfod (typically in summer, June)
			var eisteddfod = new Event
			{
				Id = Guid.NewGuid(),
				Date = new DateTime(year, 6, 15),
				EventType = "Eisteddfod",
				Notes = $"Annual Eisteddfod {year}",
				IsDeleted = false
			};
			allEvents.Add(eisteddfod);
			context.Events.Add(eisteddfod);

			// 2. Two Annual Concerts (consecutive days, July)
			var concert1 = new Event
			{
				Id = Guid.NewGuid(),
				Date = new DateTime(year, 7, 20),
				EventType = "Annual concert",
				Notes = $"Annual concert {year} - Day 1",
				IsDeleted = false
			};
			allEvents.Add(concert1);
			context.Events.Add(concert1);

			var concert2 = new Event
			{
				Id = Guid.NewGuid(),
				Date = new DateTime(year, 7, 21),
				EventType = "Annual concert",
				Notes = $"Annual concert {year} - Day 2",
				IsDeleted = false
			};
			allEvents.Add(concert2);
			context.Events.Add(concert2);

			// 3. AGM (October)
			var agm = new Event
			{
				Id = Guid.NewGuid(),
				Date = new DateTime(year, 10, 15),
				EventType = "AGM",
				Notes = $"Annual General Meeting {year}",
				IsDeleted = false
			};
			allEvents.Add(agm);
			context.Events.Add(agm);
		}

		await context.SaveChangesAsync();

		// Create attendance records for past rehearsals (80% of active members)
		foreach (var rehearsal in allRehearsals.Where(r => r.Date <= _now))
		{
			var attendanceCount = (int)Math.Ceiling(activeMembers.Count * 0.8);
			var membersToAttend = activeMembers.OrderBy(_ => _random.Next()).Take(attendanceCount).ToList();

			foreach (var member in membersToAttend)
			{
				var attendance = new Attendance
				{
					Id = Guid.NewGuid(),
					RehearsalId = rehearsal.Id,
					MemberId = member.Id,
					RecordedAt = rehearsal.Date.AddHours(1), // Recorded an hour after rehearsal
					PaidStatus = "Paid" // All members in test data have paid
				};
				context.Attendances.Add(attendance);
			}
		}

		// Create participation records for past events (80% of active members)
		foreach (var @event in allEvents.Where(e => e.Date <= _now))
		{
			var participationCount = (int)Math.Ceiling(activeMembers.Count * 0.8);
			var membersToParticipate = activeMembers.OrderBy(_ => _random.Next()).Take(participationCount).ToList();

			foreach (var member in membersToParticipate)
			{
				var participation = new Participation
				{
					Id = Guid.NewGuid(),
					EventId = @event.Id,
					MemberId = member.Id,
					RecordedAt = @event.Date.AddHours(1)
				};
				context.Participations.Add(participation);
			}
		}

		await context.SaveChangesAsync();

		// Create financial records for active members
		foreach (var member in activeMembers)
		{
			// Create annual fees for years where they were active (2023-2026)
			for (int year = 2023; year <= 2026; year++)
			{
				// Only create fee if it would be in the past or current year
				var feeDate = new DateTime(year, 1, 1);
				if (feeDate <= _now)
				{
					var fee = new Fee
					{
						Id = Guid.NewGuid(),
						MemberId = member.Id,
						FeeType = "Annual",
						Amount = settings.AnnualFee,
						FeeDate = feeDate,
						DueDate = feeDate.AddMonths(1),
						CreatedAt = feeDate
					};
					context.Fees.Add(fee);

					// Create corresponding payment (assuming all fees are paid)
					var payment = new Payment
					{
						Id = Guid.NewGuid(),
						MemberId = member.Id,
						Date = feeDate.AddDays(_random.Next(1, 30)), // Paid within 30 days
						Amount = settings.AnnualFee,
						PaymentMethod = new[] { "Cash", "Check", "Bank Transfer" }[_random.Next(3)],
						PaymentType = "Annual",
						Category = "Annual Fee",
						Notes = $"Annual fee payment for {year}",
						CreatedAt = feeDate.AddDays(_random.Next(1, 30)),
						UpdatedAt = feeDate.AddDays(_random.Next(1, 30))
					};
					context.Payments.Add(payment);
				}
			}

			// Create attendance fee records for each past rehearsal they attended
			var attendanceRecords = context.Attendances.Where(a => a.MemberId == member.Id).ToList();
			foreach (var attendance in attendanceRecords)
			{
				var rehearsal = allRehearsals.FirstOrDefault(r => r.Id == attendance.RehearsalId);
				if (rehearsal != null && rehearsal.Date <= _now)
				{
					var fee = new Fee
					{
						Id = Guid.NewGuid(),
						MemberId = member.Id,
						FeeType = "Attendance",
						Amount = settings.AttendanceFee,
						FeeDate = rehearsal.Date,
						DueDate = rehearsal.Date.AddDays(7),
						CreatedAt = rehearsal.Date
					};
					context.Fees.Add(fee);

					// Create corresponding payment
					var payment = new Payment
					{
						Id = Guid.NewGuid(),
						MemberId = member.Id,
						Date = rehearsal.Date.AddDays(_random.Next(1, 8)),
						Amount = settings.AttendanceFee,
						PaymentMethod = new[] { "Cash", "Check", "Bank Transfer" }[_random.Next(3)],
						PaymentType = "Attendance",
						Category = "Attendance Fee",
						Notes = $"Attendance fee for rehearsal on {rehearsal.Date:yyyy-MM-dd}",
						CreatedAt = rehearsal.Date.AddDays(_random.Next(1, 8)),
						UpdatedAt = rehearsal.Date.AddDays(_random.Next(1, 8))
					};
					context.Payments.Add(payment);
				}
			}
		}

		await context.SaveChangesAsync();

		// Create committee memberships for each year
		for (int year = 2023; year <= 2026; year++)
		{
			// Randomly select committee members
			var shuffledMembers = activeMembers.OrderBy(_ => _random.Next()).ToList();

			// President
			var president = shuffledMembers[0];
			context.CommitteeMemberships.Add(new CommitteeMembership
			{
				Id = Guid.NewGuid(),
				MemberId = president.Id,
				Year = year,
				Position = "President",
				IsDeleted = false,
				CreatedAt = _now,
				ModifiedAt = _now
			});

			// Secretary
			var secretary = shuffledMembers[1];
			context.CommitteeMemberships.Add(new CommitteeMembership
			{
				Id = Guid.NewGuid(),
				MemberId = secretary.Id,
				Year = year,
				Position = "Secretary",
				IsDeleted = false,
				CreatedAt = _now,
				ModifiedAt = _now
			});

			// Treasurer
			var treasurer = shuffledMembers[2];
			context.CommitteeMemberships.Add(new CommitteeMembership
			{
				Id = Guid.NewGuid(),
				MemberId = treasurer.Id,
				Year = year,
				Position = "Treasurer",
				IsDeleted = false,
				CreatedAt = _now,
				ModifiedAt = _now
			});

			// 5 Committee Members
			for (int i = 0; i < 5; i++)
			{
				var member = shuffledMembers[3 + i];
				context.CommitteeMemberships.Add(new CommitteeMembership
				{
					Id = Guid.NewGuid(),
					MemberId = member.Id,
					Year = year,
					Position = "Committee Member",
					IsDeleted = false,
					CreatedAt = _now,
					ModifiedAt = _now
				});
			}
		}

		await context.SaveChangesAsync();
	}
}
