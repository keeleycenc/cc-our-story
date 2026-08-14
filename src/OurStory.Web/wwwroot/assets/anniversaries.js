(function () {
  'use strict';

  const pageSize = 10;
  const lists = Array.from(document.querySelectorAll('[data-lazy-list]'));

  const text = (tag, className, value) => {
    const element = document.createElement(tag);
    if (className) element.className = className;
    element.textContent = value;
    return element;
  };

  const icon = (name) => {
    const svg = document.createElementNS('http://www.w3.org/2000/svg', 'svg');
    svg.setAttribute('class', 'icon');
    svg.setAttribute('aria-hidden', 'true');
    const use = document.createElementNS('http://www.w3.org/2000/svg', 'use');
    use.setAttribute('href', '#i-' + name);
    svg.append(use);
    return svg;
  };

  const thumb = (url) => {
    const holder = document.createElement('span');
    holder.className = 'anniversary-thumb';
    const image = document.createElement('img');
    image.src = url;
    image.alt = '';
    image.loading = 'lazy';
    image.decoding = 'async';
    holder.append(image);
    return holder;
  };

  const linkedTitle = (item) => {
    const heading = document.createElement('h3');
    const link = text('a', '', item.title);
    link.href = item.url;
    heading.append(link);
    return heading;
  };

  const formatDate = (value, separator) => {
    if (!value) return '—';
    const parts = value.split('-');
    return parts.join(separator || '.');
  };

  const renderTimeline = (item) => {
    const article = document.createElement('article');
    article.className = 'timeline-item kind-' + item.kind + (item.coverUrl ? ' has-cover' : '');

    const rail = document.createElement('div');
    rail.className = 'timeline-rail';
    rail.setAttribute('aria-hidden', 'true');
    rail.append(document.createElement('i'), document.createElement('span'));

    const copy = document.createElement('div');
    copy.className = 'timeline-copy';
    const titleRow = document.createElement('div');
    titleRow.className = 'timeline-title-row';
    titleRow.append(linkedTitle(item), text('span', '', item.kindName));
    if (item.repeatYearly) titleRow.append(text('em', '', '每年提醒'));
    if (item.isPrivate) titleRow.append(text('em', 'privacy-tag', '私密'));

    const date = text('time', '', formatDate(item.originalDate));
    date.dateTime = item.originalDate;
    const meta = document.createElement('div');
    meta.className = 'anniversary-meta';
    meta.append(date, text('span', '', '由 ' + item.authorName + ' 记录'));
    const note = text('p', 'timeline-note', item.summary || '这段回忆还没有写下描述。');
    copy.append(titleRow, meta, note);

    article.append(rail);
    if (item.coverUrl) article.append(thumb(item.coverUrl));
    article.append(copy, text('strong', 'timeline-day', '第 ' + item.dayNumber + ' 天'));
    return article;
  };

  const renderUpcoming = (item) => {
    const article = document.createElement('article');
    article.className = 'upcoming-card kind-' + item.kind + (item.coverUrl ? ' has-cover' : '');

    const dateParts = (item.nextDate || '').split('-');
    const date = document.createElement('time');
    date.dateTime = item.nextDate || '';
    date.append(text('b', '', dateParts[2] || '—'), text('span', '', dateParts[1] ? dateParts[1] + '月' : ''));

    const copy = document.createElement('div');
    const titleRow = document.createElement('div');
    titleRow.className = 'upcoming-title-row';
    titleRow.append(linkedTitle(item), text('span', '', item.kindName));
    if (item.isPrivate) titleRow.append(text('em', 'privacy-tag', '私密'));
    copy.append(
      titleRow,
      text('span', 'anniversary-recorder', '由 ' + item.authorName + ' 记录'),
      text('p', 'upcoming-note', item.summary || '每年这一天，都回来看看。')
    );

    article.append(date);
    if (item.coverUrl) article.append(thumb(item.coverUrl));
    article.append(copy, text('strong', '', item.daysUntil === 0 ? '今天' : item.daysUntil + ' 天'));
    return article;
  };

  lists.forEach((list) => {
    const type = list.dataset.lazyList;
    const content = list.querySelector('[data-lazy-content]');
    const footer = list.parentElement.querySelector('[data-lazy-footer="' + type + '"]');
    const status = footer.querySelector('[data-lazy-status]');
    const progress = footer.querySelector('[data-lazy-progress]');
    const total = Number(list.dataset.total || 0);
    let loaded = Number(list.dataset.loaded || 0);
    let loading = false;
    let finished = loaded >= total;
    let frame = 0;

    const setStatus = (message) => {
      status.textContent = message;
    };

    const setProgress = () => {
      progress.textContent = '已显示 ' + loaded + ' / ' + total;
    };

    const loadMore = async () => {
      if (loading || finished) return;
      loading = true;
      list.setAttribute('aria-busy', 'true');
      setStatus('正在加载更多');

      const handler = type.charAt(0).toUpperCase() + type.slice(1);
      const url = window.location.pathname + '?handler=' + handler + '&skip=' + loaded + '&take=' + pageSize;
      try {
        const response = await fetch(url, { headers: { Accept: 'application/json' } });
        if (!response.ok) throw new Error('HTTP ' + response.status);
        const payload = await response.json();
        const render = type === 'timeline' ? renderTimeline : renderUpcoming;
        payload.items.forEach((item) => content.appendChild(render(item)));
        loaded += payload.items.length;
        list.dataset.loaded = String(loaded);
        setProgress();
        finished = !payload.hasMore || loaded >= total;
        setStatus(finished ? (type === 'timeline' ? '全部回忆已显示' : '全部提醒已显示') : '向下滚动，继续加载');
      } catch (_) {
        const retry = text('button', '', '加载失败，点击重试');
        retry.type = 'button';
        retry.addEventListener('click', loadMore, { once: true });
        status.replaceChildren(retry);
      } finally {
        loading = false;
        list.setAttribute('aria-busy', 'false');
      }
    };

    list.addEventListener('scroll', () => {
      if (frame) return;
      frame = window.requestAnimationFrame(() => {
        frame = 0;
        const remaining = list.scrollHeight - list.scrollTop - list.clientHeight;
        if (remaining <= 120) loadMore();
      });
    }, { passive: true });

    setProgress();
    if (finished) setStatus(type === 'timeline' ? '全部回忆已显示' : '全部提醒已显示');
  });

  const calendar = document.querySelector('[data-anniversary-calendar]');
  if (!calendar) return;

  const dataElement = calendar.querySelector('[data-calendar-data]');
  const yearSelect = calendar.querySelector('[data-calendar-year]');
  const monthSelect = calendar.querySelector('[data-calendar-month]');
  const grid = calendar.querySelector('[data-calendar-grid]');
  const agendaWeekday = calendar.querySelector('[data-calendar-agenda-weekday]');
  const agendaDate = calendar.querySelector('[data-calendar-agenda-date]');
  const agendaCount = calendar.querySelector('[data-calendar-agenda-count]');
  const agendaPanel = calendar.querySelector('.memory-calendar-agenda');
  const agenda = calendar.querySelector('[data-calendar-agenda]');
  const weekdays = ['星期日', '星期一', '星期二', '星期三', '星期四', '星期五', '星期六'];
  const todayParts = calendar.dataset.today.split('-').map(Number);
  const today = { year: todayParts[0], month: todayParts[1], day: todayParts[2] };
  const items = JSON.parse(dataElement.textContent || '[]').map((item) => {
    const parts = item.originalDate.split('-').map(Number);
    return { ...item, year: parts[0], month: parts[1], day: parts[2] };
  });

  let viewYear = today.year;
  let viewMonth = today.month;
  let selectedDay = today.day;
  let minimumYear = Math.min(today.year, ...items.map((item) => item.year));
  let maximumYear = Math.max(today.year + 10, ...items.map((item) => item.year));

  const daysInMonth = (year, month) => new Date(year, month, 0).getDate();

  const shiftMonth = (year, month, delta) => {
    const moved = new Date(year, month - 1 + delta, 1);
    return { year: moved.getFullYear(), month: moved.getMonth() + 1 };
  };

  const recordsFor = (year, month, day) => items
    .filter((item) => {
      if (!item.repeatYearly) {
        return item.year === year && item.month === month && item.day === day;
      }

      if (year < item.year || item.month !== month) return false;
      const recurringDay = Math.min(item.day, daysInMonth(year, month));
      return recurringDay === day;
    })
    .sort((left, right) => left.title.localeCompare(right.title, 'zh-CN'));

  const fillYearOptions = () => {
    const selected = viewYear;
    yearSelect.replaceChildren();
    for (let year = minimumYear; year <= maximumYear; year += 1) {
      const option = text('option', '', year + ' 年');
      option.value = String(year);
      yearSelect.append(option);
    }
    yearSelect.value = String(selected);
  };

  const ensureYear = (year) => {
    if (year < minimumYear) minimumYear = year;
    if (year > maximumYear) maximumYear = year;
    fillYearOptions();
  };

  const renderAgenda = () => {
    const records = recordsFor(viewYear, viewMonth, selectedDay);
    const selectedDate = new Date(viewYear, viewMonth - 1, selectedDay);
    const isToday = viewYear === today.year && viewMonth === today.month && selectedDay === today.day;
    agendaPanel.classList.toggle('is-today', isToday);
    agendaWeekday.textContent = isToday ? '今天 · ' + weekdays[selectedDate.getDay()] : weekdays[selectedDate.getDay()];
    agendaDate.textContent = viewYear + ' 年 ' + viewMonth + ' 月 ' + selectedDay + ' 日';
    agendaCount.textContent = records.length ? records.length + ' 条纪念日' : '这一天还没有记录';
    agenda.replaceChildren();

    if (!records.length) {
      const empty = document.createElement('p');
      empty.className = 'calendar-agenda-empty';
      empty.append(icon('calendar-heart'), text('span', '', '这一天还没有纪念日，留给未来慢慢填写。'));
      agenda.append(empty);
      return;
    }

    records.forEach((item) => {
      const link = document.createElement('a');
      link.className = 'calendar-agenda-item kind-' + item.kind;
      link.href = item.url;

      const badge = document.createElement('span');
      badge.className = 'calendar-agenda-icon';
      badge.append(icon(item.kindIcon || 'calendar-heart'));

      const labels = [item.kindName];
      if (item.repeatYearly) labels.push('每年重复');
      if (item.isPrivate) labels.push('私密');

      const copy = document.createElement('span');
      copy.className = 'calendar-agenda-copy';
      copy.append(
        text('strong', '', item.title),
        text('span', 'calendar-agenda-tags', labels.join(' · ')),
        text('small', '', '由 ' + item.authorName + ' 记录')
      );

      link.append(badge, copy);
      agenda.append(link);
    });
  };

  /** 画一个日期格子；越界的日期用相邻月份补齐，点一下就翻过去。 */
  const buildDay = (year, month, day, outside) => {
    const records = recordsFor(year, month, day);
    const weekday = new Date(year, month - 1, day).getDay();
    const button = document.createElement('button');
    button.type = 'button';
    button.className = 'calendar-day';
    button.setAttribute('role', 'gridcell');
    button.setAttribute('aria-label', year + '年' + month + '月' + day + '日，'
      + (records.length ? records.length + '条纪念日' : '没有纪念日'));
    if (weekday === 0 || weekday === 6) button.classList.add('is-weekend');
    if (outside) button.classList.add('is-outside');
    if (records.length) button.classList.add('has-records');
    if (year === today.year && month === today.month && day === today.day) button.classList.add('is-today');
    if (!outside && day === selectedDay) button.classList.add('is-selected');
    button.append(text('span', 'calendar-day-number', String(day)));

    if (records.length && outside) {
      const dots = document.createElement('span');
      dots.className = 'calendar-day-dots';
      records.slice(0, 3).forEach((item) => dots.append(text('i', 'kind-' + item.kind, '')));
      button.append(dots);
    } else if (records.length) {
      // 格子只有两行的高度：装得下就都列出来，装不下就留一条加一句「还有几个」。
      const visible = records.length > 2 ? records.slice(0, 1) : records;
      const recordList = document.createElement('span');
      recordList.className = 'calendar-day-records';
      visible.forEach((item) => {
        const entry = document.createElement('span');
        entry.className = 'calendar-day-entry kind-' + item.kind;
        entry.title = item.title;
        entry.append(text('i', '', ''), text('b', '', item.title));
        recordList.append(entry);
      });
      if (records.length > visible.length) {
        recordList.append(text('span', 'calendar-day-more', '还有 ' + (records.length - visible.length) + ' 个'));
      }

      button.append(recordList);
    }

    button.addEventListener('click', () => {
      if (outside) {
        viewYear = year;
        viewMonth = month;
      }

      selectedDay = day;
      renderCalendar();
    });
    return button;
  };

  const renderCalendar = () => {
    ensureYear(viewYear);
    yearSelect.value = String(viewYear);
    monthSelect.value = String(viewMonth);
    selectedDay = Math.min(selectedDay, daysInMonth(viewYear, viewMonth));

    const leadingBlanks = (new Date(viewYear, viewMonth - 1, 1).getDay() + 6) % 7;
    const monthDays = daysInMonth(viewYear, viewMonth);
    // 至少五行：二月刚好从周一开始时只要四行，月历会突然矮一截，翻月份时跳得很难看。
    const rows = Math.max(5, Math.ceil((leadingBlanks + monthDays) / 7));
    const previous = shiftMonth(viewYear, viewMonth, -1);
    const following = shiftMonth(viewYear, viewMonth, 1);
    const previousDays = daysInMonth(previous.year, previous.month);
    const cells = [];

    for (let index = 0; index < rows * 7; index += 1) {
      const day = index - leadingBlanks + 1;
      if (day < 1) {
        cells.push(buildDay(previous.year, previous.month, previousDays + day, true));
      } else if (day > monthDays) {
        cells.push(buildDay(following.year, following.month, day - monthDays, true));
      } else {
        cells.push(buildDay(viewYear, viewMonth, day, false));
      }
    }

    grid.style.setProperty('--calendar-rows', String(rows));
    grid.replaceChildren(...cells);
    grid.setAttribute('aria-label', viewYear + '年' + viewMonth + '月纪念日日历');
    renderAgenda();
  };

  const firstRecordedDay = (year, month) => {
    const count = daysInMonth(year, month);
    for (let day = 1; day <= count; day += 1) {
      if (recordsFor(year, month, day).length) return day;
    }
    return 1;
  };

  const moveTo = (year, month, preferToday) => {
    while (month < 1) { year -= 1; month += 12; }
    while (month > 12) { year += 1; month -= 12; }
    viewYear = year;
    viewMonth = month;
    selectedDay = preferToday && year === today.year && month === today.month
      ? today.day
      : firstRecordedDay(year, month);
    renderCalendar();
  };

  fillYearOptions();
  yearSelect.addEventListener('change', () => moveTo(Number(yearSelect.value), viewMonth, false));
  monthSelect.addEventListener('change', () => moveTo(viewYear, Number(monthSelect.value), false));
  calendar.querySelector('[data-calendar-previous]').addEventListener('click', () => moveTo(viewYear, viewMonth - 1, false));
  calendar.querySelector('[data-calendar-next]').addEventListener('click', () => moveTo(viewYear, viewMonth + 1, false));
  calendar.querySelector('[data-calendar-today]').addEventListener('click', () => moveTo(today.year, today.month, true));
  renderCalendar();
}());
