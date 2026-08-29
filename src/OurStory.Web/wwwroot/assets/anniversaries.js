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
    holder.dataset.cover = '';
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
    if (item.calendarType === 'lunar') titleRow.append(text('em', '', '农历'));
    if (item.isPrivate) titleRow.append(text('em', 'privacy-tag', '私密'));

    const date = text('time', '', '公历 ' + formatDate(item.originalDate));
    date.dateTime = item.originalDate;
    const meta = document.createElement('div');
    meta.className = 'anniversary-meta';
    meta.append(date);
    if (item.lunarDate) meta.append(text('span', 'anniversary-lunar-date', item.lunarDate));
    meta.append(text('span', '', '由 ' + item.authorName + ' 记录'));
    const note = text('p', 'timeline-note', item.summary || '这段回忆还等着我们一起补上描述。');
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
    if (item.calendarType === 'lunar') titleRow.append(text('span', '', '农历'));
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
        if (window.ccWatchCovers) window.ccWatchCovers(content);
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

  const yearSelect = calendar.querySelector('[data-calendar-year]');
  const monthSelect = calendar.querySelector('[data-calendar-month]');
  const grid = calendar.querySelector('[data-calendar-grid]');
  const agendaWeekday = calendar.querySelector('[data-calendar-agenda-weekday]');
  const agendaDate = calendar.querySelector('[data-calendar-agenda-date]');
  const agendaCount = calendar.querySelector('[data-calendar-agenda-count]');
  const agendaPanel = calendar.querySelector('.calendar-agenda');
  const agenda = calendar.querySelector('[data-calendar-agenda]');
  const modeButtons = Array.from(calendar.querySelectorAll('[data-calendar-mode]'));
  const todayKey = calendar.dataset.today;
  let mode = 'solar';
  let payload = null;
  let selectedSolarDate = todayKey;
  let loading = false;

  const fillYearOptions = (selected, minimum, maximum) => {
    yearSelect.replaceChildren();
    for (let year = minimum; year <= maximum; year += 1) {
      const option = text('option', '', year + ' 年');
      option.value = String(year);
      yearSelect.append(option);
    }
    yearSelect.value = String(selected);
  };

  const fillMonthOptions = (months, selected) => {
    monthSelect.replaceChildren(...months.map((item) => {
      const option = text('option', '', item.label);
      option.value = item.value;
      return option;
    }));
    monthSelect.value = selected;
  };

  const renderAgenda = (cell) => {
    const records = cell.records || [];
    agendaPanel.classList.toggle('is-today', cell.isToday);
    agendaWeekday.textContent = cell.isToday ? '今天 · ' + cell.weekday : cell.weekday;
    agendaDate.textContent = mode === 'lunar' ? cell.lunarLabel : cell.solarLabel;
    const companion = mode === 'lunar' ? cell.solarLabel : cell.lunarLabel;
    agendaCount.textContent = companion + ' · ' + (records.length ? records.length + ' 条纪念日' : '暂无纪念日');
    agenda.replaceChildren();

    if (!records.length) {
      const empty = document.createElement('p');
      empty.className = 'calendar-agenda-empty';
      empty.append(icon('calendar-heart'), text('span', '', '这一天暂时没有纪念日，等我们一起写下新的故事。'));
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
      labels.push(item.calendarName);
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

  /** 画一个日期格子；主历法在上，另一套历法作为小字一直留着。 */
  const buildDay = (cell) => {
    const records = cell.records || [];
    const button = document.createElement('button');
    button.type = 'button';
    button.className = 'calendar-day';
    button.setAttribute('role', 'gridcell');
    button.setAttribute('aria-label', cell.solarLabel + '，' + cell.lunarLabel + '，'
      + (records.length ? records.length + '条纪念日' : '没有纪念日'));
    if (cell.weekend) button.classList.add('is-weekend');
    if (cell.outside) button.classList.add('is-outside');
    if (records.length) button.classList.add('has-records');
    if (cell.isToday) button.classList.add('is-today');
    if (cell.solarDate === selectedSolarDate) button.classList.add('is-selected');
    const dateCopy = document.createElement('span');
    dateCopy.className = 'calendar-day-date';
    dateCopy.append(text('span', 'calendar-day-number', cell.primary), text('small', 'calendar-day-secondary', cell.secondary));
    button.append(dateCopy);
    if (records.length) {
      const count = text('span', 'calendar-day-count', String(records.length));
      count.setAttribute('aria-label', records.length + ' 条纪念日');
      button.append(count);
    }

    if (records.length && cell.outside) {
      const dots = document.createElement('span');
      dots.className = 'calendar-day-dots';
      records.slice(0, 4).forEach((item) => dots.append(text('i', 'kind-' + item.kind, '')));
      button.append(dots);
    } else if (records.length) {
      const visible = records.slice(0, 4);
      const recordList = document.createElement('span');
      recordList.className = 'calendar-day-records';
      visible.forEach((item) => {
        const entry = document.createElement('span');
        entry.className = 'calendar-day-entry kind-' + item.kind;
        entry.title = item.title;
        entry.append(text('i', '', ''), text('b', '', item.title));
        recordList.append(entry);
      });

      button.append(recordList);
    }

    button.addEventListener('click', async () => {
      selectedSolarDate = cell.solarDate;
      if (cell.outside) {
        await requestMonth(mode === 'lunar'
          ? { year: cell.lunarYear, month: cell.lunarMonth, leap: cell.lunarLeap }
          : { year: cell.solarYear, month: cell.solarMonth, leap: false });
        return;
      }

      grid.querySelectorAll('.calendar-day.is-selected').forEach((item) => item.classList.remove('is-selected'));
      button.classList.add('is-selected');
      renderAgenda(cell);
    });
    return button;
  };

  const renderCalendar = () => {
    fillYearOptions(payload.year, payload.minimumYear, payload.maximumYear);
    fillMonthOptions(payload.months, payload.monthKey);
    modeButtons.forEach((button) => {
      const active = button.dataset.calendarMode === mode;
      button.classList.toggle('is-active', active);
      button.setAttribute('aria-pressed', String(active));
    });

    let selected = payload.cells.find((cell) => cell.solarDate === selectedSolarDate && !cell.outside);
    if (!selected) selected = payload.cells.find((cell) => cell.isToday && !cell.outside);
    if (!selected) selected = payload.cells.find((cell) => cell.records.length && !cell.outside);
    if (!selected) selected = payload.cells.find((cell) => !cell.outside);
    selectedSolarDate = selected.solarDate;
    grid.style.setProperty('--calendar-rows', String(payload.rows));
    grid.replaceChildren(...payload.cells.map(buildDay));
    grid.setAttribute('aria-label', payload.year + '年' + monthSelect.options[monthSelect.selectedIndex].textContent + (mode === 'lunar' ? '农历' : '公历') + '纪念日日历');
    renderAgenda(selected);
  };

  const requestMonth = async (target) => {
    if (loading) return;
    loading = true;
    calendar.setAttribute('aria-busy', 'true');
    const url = new URL(calendar.dataset.calendarUrl, window.location.origin);
    url.searchParams.set('calendar', mode);
    if (target && target.year) url.searchParams.set('year', target.year);
    if (target && target.month) url.searchParams.set('month', target.month);
    if (target && target.leap) url.searchParams.set('leap', 'true');
    try {
      const response = await fetch(url, { headers: { Accept: 'application/json' } });
      if (!response.ok) throw new Error('HTTP ' + response.status);
      payload = await response.json();
      renderCalendar();
    } catch (_) {
      agenda.replaceChildren(text('p', 'calendar-agenda-empty', '日历暂时加载失败，请稍后再试。'));
    } finally {
      loading = false;
      calendar.setAttribute('aria-busy', 'false');
    }
  };

  const selectedCell = () => payload && payload.cells.find((cell) => cell.solarDate === selectedSolarDate);
  modeButtons.forEach((button) => button.addEventListener('click', () => {
    const nextMode = button.dataset.calendarMode;
    if (nextMode === mode) return;
    const cell = selectedCell();
    mode = nextMode;
    requestMonth(cell && mode === 'lunar'
      ? { year: cell.lunarYear, month: cell.lunarMonth, leap: cell.lunarLeap }
      : cell ? { year: cell.solarYear, month: cell.solarMonth, leap: false } : null);
  }));
  yearSelect.addEventListener('change', () => {
    const key = monthSelect.value.split('-');
    requestMonth({ year: Number(yearSelect.value), month: Number(key[0]), leap: key.length === 2 });
  });
  monthSelect.addEventListener('change', () => {
    const key = monthSelect.value.split('-');
    requestMonth({ year: Number(yearSelect.value), month: Number(key[0]), leap: key.length === 2 });
  });
  calendar.querySelector('[data-calendar-previous]').addEventListener('click', () => requestMonth(payload.previous));
  calendar.querySelector('[data-calendar-next]').addEventListener('click', () => requestMonth(payload.next));
  calendar.querySelector('[data-calendar-today]').addEventListener('click', () => {
    selectedSolarDate = todayKey;
    requestMonth(null);
  });
  requestMonth(null);
}());
