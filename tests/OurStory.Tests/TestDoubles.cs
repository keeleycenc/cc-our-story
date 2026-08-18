// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using OurStory.Core;
using OurStory.Core.Configuration;
using OurStory.Core.Entities;
using OurStory.Core.Models;
using OurStory.Core.Options;
using OurStory.Core.Time;
using OurStory.Data;
using OurStory.Services.HeartPoints;
using OurStory.Services.LlmAtmosphere;
using OurStory.Services.Moments;
using OurStory.Services.Notifications;
using OurStory.Services.Settings;

namespace OurStory.Tests;

/// <summary>
/// 需要数据库的服务测试共用的几个替身
/// </summary>
internal static class TestDoubles {
    /// <summary>一个只属于这次测试的内存库。</summary>
    public static OurStoryDbContext Database(string name) =>
        new(new DbContextOptionsBuilder<OurStoryDbContext>()
            .UseInMemoryDatabase(name + "-" + Guid.NewGuid().ToString("n"))
            .Options);

    /// <summary>没配时区，等于 UTC，断言时间时不用跟着机器跑。</summary>
    public static SiteClock Clock() =>
        new(new ActiveConfiguration(new ConfigurationStore("."), new OurStoryConfiguration()));

    /// <summary>使用与站点相同的 Markdown 渲染规则。</summary>
    public static IMarkdownRenderer Markdown() => new MarkdownRenderer();

    /// <summary>心意流水不参与断言时给一份空实现，省得每处都拼一套。</summary>
    public static IHeartPointService NoPoints() => new HeartPointStub();

    /// <summary>只把排队的通知收进列表，不真的往外发。</summary>
    public static NotificationQueueSpy Notifications() => new();

    /// <summary>氛围组不参与断言时给一份只记账的替身。</summary>
    public static AtmosphereSchedulerSpy Atmosphere() => new();

    /// <summary>指定一份氛围组配置，用来测概率、延迟和上锁那几条规矩。</summary>
    public static ActiveConfiguration Configuration(LlmAtmosphereOptions? atmosphere = null) =>
        new(new ConfigurationStore("."), new OurStoryConfiguration {
            LlmAtmosphere = atmosphere ?? new LlmAtmosphereOptions()
        });

    /// <summary>时钟归测试管，攒单那类靠计时器的行为不用真的等。</summary>
    public static FakeTimeProvider Time() => new();
}

/// <summary>
/// 一个真的 SQLite 库，只是建在内存里。
///
/// 心意和商城要靠唯一索引挡重复发放、靠事务保证扣心意和改状态一起生效，
/// 还用到了 ExecuteUpdate —— 这三样 InMemory provider 都做不到，
/// 换成 SQLite 才测得出真实行为
/// </summary>
internal sealed class SqliteHarness : IAsyncDisposable {
    private readonly SqliteConnection _connection;

    private SqliteHarness(SqliteConnection connection, OurStoryDbContext db) {
        _connection = connection;
        Db = db;
    }

    /// <summary>这次测试用的数据库上下文。</summary>
    public OurStoryDbContext Db { get; }

    /// <summary>建一个空库，表结构照实体映射生成。</summary>
    /// <param name="createSchema">
    /// 建好表再返回。要测启动流程的话传 false —— 那条路自己会跑迁移，
    /// 表已经建好了再跑一遍会撞车
    /// </param>
    public static SqliteHarness Create(bool createSchema = true) {
        // 连接一关内存库就没了，所以这条连接得一直开着
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        var db = new OurStoryDbContext(new DbContextOptionsBuilder<OurStoryDbContext>()
            .UseSqlite(connection)
            .Options);

        if (createSchema) {
            _ = db.Database.EnsureCreated();
        }

        return new SqliteHarness(connection, db);
    }

    /// <summary>放两个人进去，返回男主和女主的主键。</summary>
    public async Task<(int BoyId, int GirlId)> SeedCoupleAsync() {
        var boy = new User { UserName = "boy", Role = UserRole.Boy, PasswordHash = "test" };
        var girl = new User { UserName = "girl", Role = UserRole.Girl, PasswordHash = "test" };

        Db.Users.AddRange(boy, girl);
        _ = await Db.SaveChangesAsync();
        return (boy.Id, girl.Id);
    }

    /// <summary>释放数据库上下文与底层连接。</summary>
    public async ValueTask DisposeAsync() {
        await Db.DisposeAsync();
        await _connection.DisposeAsync();
    }
}

/// <summary>不碰设置表，站点配置固定给一份；GetRaw / SetRaw 存在内存里。</summary>
internal sealed class SettingsStub(SiteSettings? settings = null) : ISettingsService {
    private readonly Dictionary<string, string> _raw = new(StringComparer.Ordinal);
    private readonly SiteSettings _settings = settings ?? new SiteSettings { BoyName = "男主", GirlName = "女主" };

    public Task<SiteSettings> GetAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(_settings);

    public Task SaveAsync(SiteSettings settings, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task<string?> GetRawAsync(string key, CancellationToken cancellationToken = default) =>
        Task.FromResult(_raw.TryGetValue(key, out var value) ? value : null);

    public Task SetRawAsync(string key, string value, CancellationToken cancellationToken = default) {
        _raw[key] = value;
        return Task.CompletedTask;
    }
}

/// <summary>
/// 把排队的通知留在手边，测试可以直接翻这份清单
/// </summary>
/// <remarks>发通知是「做完这件事顺带的动静」，不该让业务测试真的去连推送服务。</remarks>
internal sealed class NotificationQueueSpy : INotificationQueue {
    /// <summary>按排队顺序记下的所有通知。</summary>
    public List<NotificationRequest> Sent { get; } = [];

    public bool Enqueue(NotificationRequest request) {
        Sent.Add(request);
        return true;
    }

    public async IAsyncEnumerable<NotificationRequest> ReadAllAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default) {
        foreach (var request in Sent) {
            cancellationToken.ThrowIfCancellationRequested();
            yield return request;
        }

        await Task.CompletedTask;
    }
}

/// <summary>
/// 把排给氛围组的待办留在手边，测试直接翻这份清单
/// </summary>
/// <remarks>真的去掷骰子、连模型，这类业务测试就没法断言了。</remarks>
internal sealed class AtmosphereSchedulerSpy : ILlmAtmosphereScheduler {
    /// <summary>按顺序记下的所有待办。</summary>
    public List<LlmAtmosphereTrigger> Scheduled { get; } = [];

    /// <summary>发布触发被叫到时的入参。</summary>
    public List<(int MomentId, bool IsProtected)> Published { get; } = [];

    /// <summary>评论触发被叫到时的入参。</summary>
    public List<(int MomentId, int CommentId, string? RepliedMemberId, bool IsProtected)> Commented { get; } = [];

    public int Pending => Scheduled.Count;

    public void OnMomentPublished(int momentId, bool isProtected) =>
        Published.Add((momentId, isProtected));

    public void OnCommentAdded(int momentId, int commentId, string? repliedMemberId, bool isProtected) =>
        Commented.Add((momentId, commentId, repliedMemberId, isProtected));

    public bool Schedule(LlmAtmosphereTrigger trigger) {
        Scheduled.Add(trigger);
        return true;
    }

    public IReadOnlyList<LlmAtmosphereTrigger> TakeDue() {
        var due = Scheduled.ToList();
        Scheduled.Clear();
        return due;
    }
}

/// <summary>
/// 按事先摆好的答案作答的模型，一次调用取走一个
/// </summary>
/// <remarks>答案用完之后一律回 <see cref="ResponsesFailure.Unreachable"/>，免得测试悄悄多调一次还看不出来。</remarks>
internal sealed class ResponsesClientStub(params ResponsesResult[] answers) : IResponsesClient {
    private readonly Queue<ResponsesResult> _answers = new(answers);

    /// <summary>每次调用收到的请求，按先后顺序。</summary>
    public List<ResponsesRequest> Requests { get; } = [];

    public Task<ResponsesResult> CompleteAsync(ResponsesRequest request, CancellationToken cancellationToken = default) {
        Requests.Add(request);

        return Task.FromResult(_answers.Count > 0
            ? _answers.Dequeue()
            : ResponsesResult.Failed(ResponsesFailure.Unreachable));
    }
}

/// <summary>按需交出几张假图，不碰磁盘也不连 OSS。</summary>
internal sealed class MomentImageSourceStub(params string[] urls) : IMomentImageSource {
    public Task<IReadOnlyList<ResponsesImage>> CollectAsync(
        Moment moment,
        int max,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<ResponsesImage>>([.. urls.Take(max).Select(url => new ResponsesImage(url))]);
}

/// <summary>不记账的心意服务，给那些和心意无关的测试用。</summary>
internal sealed class HeartPointStub : IHeartPointService {
    public Task<int> GetBalanceAsync(int userId, CancellationToken cancellationToken = default) =>
        Task.FromResult(0);

    public Task<IReadOnlyList<HeartPointBalance>> GetBalancesAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<HeartPointBalance>>([]);

    public Task<PagedList<HeartPointRecord>> GetRecordsAsync(int userId, int page, int pageSize, CancellationToken cancellationToken = default) =>
        Task.FromResult(PagedList<HeartPointRecord>.Empty(pageSize));

    public Task<int> AwardDailyAsync(int userId, HeartPointReason reason, string day, CancellationToken cancellationToken = default) =>
        Task.FromResult(0);

    public Task<HeartPointBackfillResult> BackfillAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(new HeartPointBackfillResult(false, 0, 0));

    public Task<bool> IsBackfilledAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(false);
}

internal sealed class FakeTimeProvider : TimeProvider {
    private readonly Lock _gate = new();
    private readonly List<FakeTimer> _timers = [];
    private DateTimeOffset _now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    /// <summary>当前这一刻，只有 <see cref="Advance"/> 能推动它。</summary>
    public override DateTimeOffset GetUtcNow() {
        lock (_gate) {
            return _now;
        }
    }

    /// <summary>建一个听这份时钟的计时器。</summary>
    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period) {
        var timer = new FakeTimer(this, callback, state);

        lock (_gate) {
            _timers.Add(timer);
        }

        _ = timer.Change(dueTime, period);
        return timer;
    }

    /// <summary>
    /// 把时钟往前拨，路上到点的计时器按先后依次响。
    /// </summary>
    /// <param name="span">往前拨多久</param>
    public void Advance(TimeSpan span) {
        var target = GetUtcNow() + span;

        // 回调里可能又把自己往后排（每点一下都重新计时），所以一轮一轮地找最近的那个
        while (Next(target) is { } timer) {
            lock (_gate) {
                _now = timer.DueAt!.Value;
            }

            timer.Fire();
        }

        lock (_gate) {
            _now = target;
        }
    }

    private void Remove(FakeTimer timer) {
        lock (_gate) {
            _ = _timers.Remove(timer);
        }
    }

    /// <summary>截止时刻之前最早响的那个计时器；都还没到点就返回 null。</summary>
    private FakeTimer? Next(DateTimeOffset until) {
        lock (_gate) {
            return _timers
                .Where(timer => timer.DueAt is { } due && due <= until)
                .OrderBy(timer => timer.DueAt!.Value)
                .FirstOrDefault();
        }
    }

    private sealed class FakeTimer(FakeTimeProvider owner, TimerCallback callback, object? state) : ITimer {
        private TimeSpan _period = Timeout.InfiniteTimeSpan;

        /// <summary>下次该响的时刻；没排期时为 null。</summary>
        public DateTimeOffset? DueAt { get; private set; }

        public bool Change(TimeSpan dueTime, TimeSpan period) {
            _period = period;
            DueAt = dueTime == Timeout.InfiniteTimeSpan ? null : owner.GetUtcNow() + dueTime;
            return true;
        }

        /// <summary>响一次；设了间隔就接着排下一次。</summary>
        public void Fire() {
            DueAt = _period == Timeout.InfiniteTimeSpan ? null : DueAt + _period;
            callback(state);
        }

        public void Dispose() => owner.Remove(this);

        public ValueTask DisposeAsync() {
            Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
