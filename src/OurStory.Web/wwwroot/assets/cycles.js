(function () {
  'use strict';

  var weekdays = ['星期日', '星期一', '星期二', '星期三', '星期四', '星期五', '星期六'];

  /* ---------------------------------------------------------------- 弹层 */

  var activeDialog = null;
  var dialogTrigger = null;

  function closeDialog(dialog) {
    if (!dialog) return;
    dialog.classList.remove('is-open');
    dialog.setAttribute('aria-hidden', 'true');
    if (activeDialog === dialog) activeDialog = null;
    if (!document.querySelector('.cycle-dialog.is-open')) document.body.classList.remove('modal-open');
    if (dialogTrigger) dialogTrigger.focus();
  }

  function openDialog(name, trigger) {
    var dialog = document.querySelector('[data-cycle-dialog="' + name + '"]');
    if (!dialog) return null;
    if (activeDialog) closeDialog(activeDialog);
    activeDialog = dialog;
    dialogTrigger = trigger || null;
    dialog.classList.add('is-open');
    dialog.setAttribute('aria-hidden', 'false');
    document.body.classList.add('modal-open');
    var target = dialog.querySelector('input:not([type="hidden"]):not([type="radio"]):not([type="checkbox"]), textarea, button:not([data-cycle-close])');
    if (target) window.setTimeout(function () { target.focus(); }, 20);
    return dialog;
  }

  document.addEventListener('click', function (event) {
    var opener = event.target.closest('[data-cycle-open]');
    if (opener) {
      openDialog(opener.getAttribute('data-cycle-open'), opener);
      return;
    }
    var closer = event.target.closest('[data-cycle-close]');
    if (closer) closeDialog(closer.closest('[data-cycle-dialog]'));
  });

  document.addEventListener('keydown', function (event) {
    if (event.key !== 'Escape') return;
    if (activeDialog) closeDialog(activeDialog);
    else if (editor && !editor.hidden) closeDayEditor(true);
  });

  document.querySelectorAll('.cycle-form').forEach(function (form) {
    var start = form.querySelector('input[name="startDate"]');
    var end = form.querySelector('input[name="endDate"]');
    if (!start || !end) return;

    var sync = function () {
      end.min = start.value;
      end.setCustomValidity(end.value && start.value && end.value < start.value ? '结束日期不能早于开始日期' : '');
    };
    start.addEventListener('change', sync);
    end.addEventListener('change', sync);
    sync();
  });

  document.querySelectorAll('.cycle-page form').forEach(function (form) {
    form.addEventListener('submit', function (event) {
      if (event.defaultPrevented) return;
      var button = form.querySelector('button[type="submit"]');
      if (!button || !form.checkValidity()) return;
      window.setTimeout(function () { button.disabled = true; }, 0);
    });
  });

  /* ------------------------------------------------------------ 小结折叠 */

  function measureSummary(box) {
    var text = box.querySelector('[data-summary-text]');
    var toggle = box.querySelector('[data-summary-toggle]');
    if (!text || !toggle || box.classList.contains('is-open')) return;
    toggle.hidden = text.scrollHeight <= text.clientHeight + 1;
  }

  function measureSummaries(root) {
    (root || document).querySelectorAll('[data-summary-box]').forEach(measureSummary);
  }

  function summaryBox(className, text) {
    var box = element('div', 'cycle-summary-box');
    box.setAttribute('data-summary-box', '');

    var body = element('p', className, text);
    body.setAttribute('data-summary-text', '');
    box.append(body);

    var toggle = element('button', 'cycle-summary-toggle');
    toggle.type = 'button';
    toggle.hidden = true;
    toggle.setAttribute('aria-expanded', 'false');
    toggle.setAttribute('data-summary-toggle', '');
    toggle.innerHTML = '<svg class="icon" aria-hidden="true"><use href="#i-chevron-down"></use></svg>'
      + '<span data-summary-label>展开</span>';
    box.append(toggle);

    return box;
  }

  document.addEventListener('click', function (event) {
    var toggle = event.target.closest('[data-summary-toggle]');
    if (!toggle) return;

    var box = toggle.closest('[data-summary-box]');
    if (!box) return;

    var open = box.classList.toggle('is-open');
    toggle.setAttribute('aria-expanded', open ? 'true' : 'false');
    var label = toggle.querySelector('[data-summary-label]');
    if (label) label.textContent = open ? '收起' : '展开';
  });

  measureSummaries();
  if (document.fonts && document.fonts.ready) {
    document.fonts.ready.then(function () { measureSummaries(); });
  }

  var summaryTimer = 0;
  window.addEventListener('resize', function () {
    window.clearTimeout(summaryTimer);
    summaryTimer = window.setTimeout(function () { measureSummaries(); }, 150);
  });

  /* ---------------------------------------------------------------- 日历 */

  var calendar = document.querySelector('[data-cycle-calendar]');
  if (!calendar) return;

  var grid = calendar.querySelector('[data-calendar-grid]');
  var yearSelect = calendar.querySelector('[data-calendar-year]');
  var monthSelect = calendar.querySelector('[data-calendar-month]');
  var agendaBody = calendar.querySelector('[data-agenda-body]');
  var agendaDate = calendar.querySelector('[data-agenda-date]');
  var agendaWeekday = calendar.querySelector('[data-agenda-weekday]');
  var agendaPhase = calendar.querySelector('[data-agenda-phase]');
  var agendaPanel = calendar.querySelector('.cycle-agenda');
  var agendaEdit = calendar.querySelector('[data-agenda-edit]');
  var endpoint = calendar.getAttribute('data-calendar-url');
  var today = calendar.getAttribute('data-today');
  var currentData = null;
  var selected = null;
  var requestNumber = 0;

  for (var month = 1; month <= 12; month += 1) {
    var monthOption = document.createElement('option');
    monthOption.value = month;
    monthOption.textContent = month + ' 月';
    monthSelect.appendChild(monthOption);
  }

  function parseDate(value) {
    var parts = value.split('-').map(Number);
    return new Date(parts[0], parts[1] - 1, parts[2]);
  }

  function fullDate(value) {
    var date = parseDate(value);
    return date.getFullYear() + ' 年 ' + (date.getMonth() + 1) + ' 月 ' + date.getDate() + ' 日';
  }

  function element(tag, className, text) {
    var node = document.createElement(tag);
    if (className) node.className = className;
    if (text !== undefined && text !== null) node.textContent = text;
    return node;
  }

  function fillYears(minimum, maximum, current) {
    if (Number(yearSelect.dataset.minimum) === minimum && Number(yearSelect.dataset.maximum) === maximum) {
      yearSelect.value = current;
      return;
    }
    yearSelect.replaceChildren();
    for (var year = minimum; year <= maximum; year += 1) {
      var option = document.createElement('option');
      option.value = year;
      option.textContent = year + ' 年';
      yearSelect.appendChild(option);
    }
    yearSelect.dataset.minimum = minimum;
    yearSelect.dataset.maximum = maximum;
    yearSelect.value = current;
  }

  /* ------------------------------------------------------------ 右侧详情 */

  function factRow(label, value) {
    var row = element('div');
    row.append(element('dt', null, label), element('dd', null, value));
    return row;
  }

  function isRegularLog(entry) {
    return Boolean(entry.flow || entry.mood || entry.pain
      || (entry.symptomNames && entry.symptomNames.length) || entry.note);
  }

  function renderAgenda(day) {
    if (selected && selected.date !== day.date) closeDayEditor(false);
    selected = day;
    grid.querySelectorAll('.calendar-day').forEach(function (button) {
      var isSelected = button.dataset.date === day.date;
      button.classList.toggle('is-selected', isSelected);
      button.setAttribute('aria-selected', isSelected ? 'true' : 'false');
    });

    var date = parseDate(day.date);
    agendaDate.textContent = fullDate(day.date);
    agendaWeekday.textContent = (day.isToday ? '今天 · ' : '') + weekdays[date.getDay()];
    agendaPanel.classList.toggle('is-today', day.isToday);

    agendaPhase.replaceChildren();
    agendaPhase.append(element('b', 'cycle-phase phase-' + day.phase, day.phaseName));
    if (day.periodDay) agendaPhase.append(element('span', null, '经期第 ' + day.periodDay + ' 天'));
    else if (day.dayOfCycle) agendaPhase.append(element('span', null, '周期第 ' + day.dayOfCycle + ' 天'));

    agendaBody.replaceChildren();
    agendaBody.append(element('p', 'cycle-agenda-hint', day.phaseHint));

    if (day.record) {
      var card = element('article', 'cycle-agenda-record');
      card.append(element('strong', null, day.record.range));

      var tags = element('div', 'cycle-tags');
      day.record.tags.forEach(function (tag) {
        tags.append(element('span', 'cycle-tag tone-' + tag.tone, tag.text));
      });
      card.append(tags);

      if (day.record.summary) card.append(summaryBox('cycle-agenda-summary', day.record.summary));
      if (day.record.note) card.append(element('blockquote', null, day.record.note));
      agendaBody.append(card);
    }

    var dayLogs = day.logs || [];
    dayLogs.forEach(function (entry) {
      var log = element('article', 'cycle-agenda-log');
      var regular = isRegularLog(entry);
      if (entry.isIntimate) log.classList.add('is-intimate');

      var head = element('header', 'cycle-agenda-log-head');
      var kinds = element('div', 'cycle-agenda-log-kinds');
      if (regular) kinds.append(element('span', 'is-regular', '日常记录'));
      if (entry.isIntimate) kinds.append(element('span', 'is-intimacy', '亲密记录'));
      head.append(kinds);
      if (entry.isIntimate) {
        var count = Math.max(1, Number(entry.intimacyCount) || 1);
        var hearts = element('span', 'cycle-agenda-intimacy-hearts');
        hearts.setAttribute('role', 'img');
        hearts.setAttribute('aria-label', '亲密记录 ' + count + ' 次');
        for (var heartIndex = 0; heartIndex < count; heartIndex += 1) {
          var heart = element('span', null, '♥');
          heart.setAttribute('aria-hidden', 'true');
          hearts.append(heart);
        }
        head.append(hearts);
      }
      log.append(head);

      var facts = element('dl', 'cycle-agenda-facts');
      if (entry.flow) facts.append(factRow('经量', entry.flowName));
      if (entry.mood) facts.append(factRow('心情', entry.moodName));
      if (entry.pain) facts.append(factRow('不适', entry.painName));
      if (entry.symptomNames.length) facts.append(factRow('身体状况', entry.symptomNames.join('、')));
      if (entry.isIntimate) {
        if (entry.protectionName !== '未记录') facts.append(factRow('安全措施', entry.protectionName));
        if (entry.outcomeName !== '未记录') facts.append(factRow('结束方式', entry.outcomeName));
      }
      if (facts.childElementCount) log.append(facts);
      else if (entry.note) log.classList.add('is-note-only');
      if (entry.note) log.append(element('p', 'cycle-agenda-note', entry.note));

      var meta = element('footer', 'cycle-agenda-log-meta');
      meta.append(element('span', null, '由 ' + entry.recordedBy + ' 记录'));
      var recordedAt = element('time', null, entry.recordedAtText);
      recordedAt.setAttribute('datetime', entry.recordedAt);
      meta.append(recordedAt);
      log.append(meta);
      agendaBody.append(log);
    });
    if (!dayLogs.length && !day.isFuture) {
      agendaBody.append(element('p', 'cycle-agenda-empty', '这一天暂时没有补充记录，双方都可以继续填写。'));
    }

    agendaEdit.hidden = day.isFuture;
    if (day.isFuture) closeDayEditor(false);
    measureSummaries(agendaBody);
  }

  /* ------------------------------------------------------------ 月历格子 */

  function renderDay(day) {
    var button = element('button', 'calendar-day phase-' + day.phase);
    button.type = 'button';
    button.setAttribute('role', 'gridcell');
    button.dataset.date = day.date;
    var logs = day.logs || [];
    var hasRegularLog = logs.some(isRegularLog);
    var hasIntimacyLog = logs.some(function (entry) { return entry.isIntimate; });
    var intimacyCount = logs.reduce(function (total, entry) {
      return total + (entry.isIntimate ? Math.max(1, Number(entry.intimacyCount) || 1) : 0);
    }, 0);
    var hasJointRecord = day.jointRecord;
    var ariaLabel = fullDate(day.date) + ' ' + day.phaseName;
    if (hasRegularLog) ariaLabel += '，有日常记录';
    if (hasIntimacyLog) ariaLabel += '，有亲密记录 ' + intimacyCount + ' 次';
    if (hasJointRecord) ariaLabel += '，双方共同记录';
    button.setAttribute('aria-label', ariaLabel);
    button.setAttribute('aria-selected', 'false');
    if (!day.inMonth) button.classList.add('is-outside');
    if (day.isToday) button.classList.add('is-today');
    if (day.isFuture) button.classList.add('is-future');
    if (hasIntimacyLog) button.classList.add('has-intimacy');
    if (day.periodDay) button.classList.add('has-records');
    if (day.periodStart) button.classList.add('is-period-start');
    if (day.periodEnd) button.classList.add('is-period-end');
    if (day.expectedStart) button.classList.add('is-expected');

    var label = '';
    if (day.expectedStart) label = '预计';
    else if (day.periodStart) label = '开始';
    else if (day.periodEnd) label = '结束';
    else if (day.phase === 'ovulation') label = '排卵';
    else if (day.periodDay) label = '第 ' + day.periodDay + ' 天';

    var date = element('span', 'calendar-day-date');
    var dayNumber = element('span', 'calendar-day-number');
    if (hasIntimacyLog) {
      var heartShape = document.createElementNS('http://www.w3.org/2000/svg', 'svg');
      heartShape.setAttribute('viewBox', '0 0 24 24');
      heartShape.setAttribute('aria-hidden', 'true');
      var heartPath = document.createElementNS('http://www.w3.org/2000/svg', 'path');
      heartPath.setAttribute('d', 'M12 21.35 10.55 20.03C5.4 15.36 2 12.28 2 8.5 2 5.42 4.42 3 7.5 3A6.2 6.2 0 0 1 12 5.09 6.2 6.2 0 0 1 16.5 3C19.58 3 22 5.42 22 8.5c0 3.78-3.4 6.86-8.55 11.54Z');
      heartShape.append(heartPath);
      dayNumber.append(heartShape, element('span', 'calendar-day-value', day.day));
    } else {
      dayNumber.append(element('span', 'calendar-day-value', day.day));
    }
    date.append(dayNumber);
    if (label) date.append(element('small', 'calendar-day-secondary', label));
    button.append(date);

    var marks = element('span', 'cycle-day-marks');
    marks.setAttribute('aria-hidden', 'true');
    if (hasJointRecord) marks.append(element('i', 'is-joint'));
    else if (hasRegularLog) marks.append(element('i', 'is-log'));
    if (marks.childElementCount) button.append(marks);

    button.addEventListener('click', function () {
      if (!day.inMonth) {
        var jump = parseDate(day.date);
        loadMonth(jump.getFullYear(), jump.getMonth() + 1, day.date);
        return;
      }
      renderAgenda(day);
    });
    button.addEventListener('dblclick', function () {
      if (day.inMonth && !day.isFuture) openDayEditor(day);
    });
    return button;
  }

  function render(data, preferred) {
    currentData = data;
    fillYears(data.minimumYear, data.maximumYear, data.year);
    monthSelect.value = data.month;
    grid.style.setProperty('--calendar-rows', data.rows);
    grid.replaceChildren();
    data.days.forEach(function (day) { grid.appendChild(renderDay(day)); });

    var pick = data.days.find(function (day) { return day.date === preferred && day.inMonth; })
      || data.days.find(function (day) { return day.date === today && day.inMonth; })
      || data.days.find(function (day) { return day.periodDay && day.inMonth; })
      || data.days.find(function (day) { return day.inMonth; });
    if (pick) renderAgenda(pick);
  }

  async function loadMonth(year, month, preferred) {
    var own = ++requestNumber;
    calendar.setAttribute('aria-busy', 'true');
    try {
      var url = new URL(endpoint, window.location.href);
      url.searchParams.set('year', year);
      url.searchParams.set('month', month);
      var response = await fetch(url.toString(), { headers: { 'X-Requested-With': 'XMLHttpRequest' } });
      if (!response.ok) throw new Error('日历加载失败');
      var data = await response.json();
      if (own !== requestNumber) return;
      render(data, preferred || (selected && selected.date));
    } catch (error) {
      if (own !== requestNumber) return;
      agendaBody.replaceChildren(element('p', 'cycle-agenda-empty', '日历加载失败，请稍后重试。'));
      agendaDate.textContent = '日历暂时无法加载';
      agendaWeekday.textContent = '';
      agendaPhase.replaceChildren();
      agendaEdit.hidden = true;
    } finally {
      if (own === requestNumber) calendar.removeAttribute('aria-busy');
    }
  }

  /* ------------------------------------------------------- 补充某一天 */

  var editor = calendar.querySelector('[data-day-editor]');
  var editorCloseTimer = null;
  var editorCloseDelay = window.matchMedia('(prefers-reduced-motion: reduce)').matches ? 0 : 240;

  function syncSecondarySymptoms(form) {
    var primary = form.querySelector('[data-primary-symptom]');
    var primaryValue = primary ? Number(primary.value) : 0;
    form.querySelectorAll('[data-secondary-symptom]').forEach(function (box) {
      var isPrimary = primaryValue !== 0 && Number(box.value) === primaryValue;
      if (isPrimary) box.checked = false;
      box.closest('label').hidden = isPrimary;
    });
  }

  function syncModule(module) {
    var toggle = module.querySelector('[data-module-toggle]');
    var body = module.querySelector('[data-module-body]');
    if (!toggle || !body) return;
    var open = toggle.checked;
    body.hidden = !open;
    toggle.setAttribute('aria-expanded', open ? 'true' : 'false');
    module.classList.toggle('is-open', open);
    body.querySelectorAll('input, select, textarea').forEach(function (input) {
      input.disabled = !open;
    });
  }

  function syncModules(form) {
    form.querySelectorAll('[data-log-module]').forEach(syncModule);
    syncSecondarySymptoms(form);
  }

  function openDayEditor(day) {
    if (!editor) return;
    if (editorCloseTimer) {
      window.clearTimeout(editorCloseTimer);
      editorCloseTimer = null;
    }
    var form = editor.querySelector('form');
    form.reset();

    form.querySelector('[data-day-date]').value = day.date;
    editor.querySelector('[data-day-title]').textContent = '补充 ' + fullDate(day.date);
    editor.querySelector('[data-day-hint]').textContent = day.periodDay
      ? '经期第 ' + day.periodDay + ' 天 · ' + day.phaseName
      : day.phaseName + ' · ' + day.phaseHint;

    syncModules(form);

    editor.hidden = false;
    agendaEdit.setAttribute('aria-expanded', 'true');
    window.requestAnimationFrame(function () {
      editor.classList.add('is-open');
      editor.scrollIntoView({ behavior: 'smooth', block: 'nearest' });
      editor.focus({ preventScroll: true });
    });
  }

  function closeDayEditor(restoreFocus) {
    if (!editor || editor.hidden) return;
    if (editorCloseTimer) window.clearTimeout(editorCloseTimer);
    editor.classList.remove('is-open');
    agendaEdit.setAttribute('aria-expanded', 'false');
    editorCloseTimer = window.setTimeout(function () {
      if (!editor.classList.contains('is-open')) editor.hidden = true;
      editorCloseTimer = null;
    }, editorCloseDelay);
    if (restoreFocus && !agendaEdit.hidden) agendaEdit.focus();
  }

  agendaEdit.addEventListener('click', function () {
    if (!selected) return;
    if (editor.classList.contains('is-open')) closeDayEditor(true);
    else openDayEditor(selected);
  });

  editor.querySelectorAll('[data-day-editor-cancel]').forEach(function (button) {
    button.addEventListener('click', function () { closeDayEditor(true); });
  });

  editor.querySelector('[data-primary-symptom]').addEventListener('change', function (event) {
    syncSecondarySymptoms(event.currentTarget.form);
  });

  editor.querySelectorAll('[data-module-toggle]').forEach(function (toggle) {
    toggle.addEventListener('change', function (event) {
      syncModule(event.currentTarget.closest('[data-log-module]'));
      syncSecondarySymptoms(event.currentTarget.form);
    });
  });

  editor.querySelector('form').addEventListener('submit', function (event) {
    if (!event.defaultPrevented && event.currentTarget.checkValidity()) closeDayEditor(false);
  });

  /* -------------------------------------------------------------- 切月 */

  calendar.querySelector('[data-calendar-previous]').addEventListener('click', function () {
    if (!currentData) return;
    var previous = parseDate(currentData.previousMonth);
    loadMonth(previous.getFullYear(), previous.getMonth() + 1);
  });
  calendar.querySelector('[data-calendar-next]').addEventListener('click', function () {
    if (!currentData) return;
    var next = parseDate(currentData.nextMonth);
    loadMonth(next.getFullYear(), next.getMonth() + 1);
  });
  calendar.querySelector('[data-calendar-today]').addEventListener('click', function () {
    var now = parseDate(today);
    loadMonth(now.getFullYear(), now.getMonth() + 1, today);
  });
  yearSelect.addEventListener('change', function () { loadMonth(Number(yearSelect.value), Number(monthSelect.value)); });
  monthSelect.addEventListener('change', function () { loadMonth(Number(yearSelect.value), Number(monthSelect.value)); });

  loadMonth(Number(calendar.dataset.year), Number(calendar.dataset.month), today);
}());
