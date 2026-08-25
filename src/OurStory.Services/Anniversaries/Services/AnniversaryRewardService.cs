// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

using Microsoft.EntityFrameworkCore;
using OurStory.Core;
using OurStory.Core.Models;
using OurStory.Core.Time;
using OurStory.Data;
using OurStory.Services.HeartPoints;
using System.Globalization;

namespace OurStory.Services.Anniversaries;

internal class AnniversaryRewardService(
    OurStoryDbContext db,
    IHeartPointService heartPoints) : IAnniversaryRewardService {
    public async Task<AnniversaryRewardResult> AwardForDayAsync(DateOnly day, CancellationToken cancellationToken = default) {
        var dayKey = day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        var owners = await db.Users
            .Where(user => user.Role == UserRole.Boy || user.Role == UserRole.Girl)
            .Select(user => user.Id)
            .ToListAsync(cancellationToken);

        if (owners.Count == 0) {
            return new AnniversaryRewardResult(dayKey, 0, 0, 0);
        }

        var items = await db.Anniversaries.AsNoTracking().ToListAsync(cancellationToken);

        var due = items
            .Select(item => AnniversaryTimeline.Calculate(item, day))
            .Where(occurrence => occurrence.IsToday)
            .ToList();

        var entries = 0;
        var total = 0;

        foreach (var occurrence in due) {
            var amount = HeartPointRules.AnniversaryReward(occurrence.Kind);

            foreach (var userId in owners) {
                var awarded = await heartPoints.AwardOnceAsync(
                    userId,
                    HeartPointReason.AnniversaryDay,
                    $"anniversary-day:{dayKey}:{occurrence.Id}",
                    amount,
                    $"纪念日 · {occurrence.Title}",
                    cancellationToken);

                if (awarded > 0) {
                    entries++;
                    total += awarded;
                }
            }
        }

        return new AnniversaryRewardResult(dayKey, due.Count, entries, total);
    }
}
