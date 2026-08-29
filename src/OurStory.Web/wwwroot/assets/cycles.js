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
    if (event.key === 'Escape' && activeDialog) closeDialog(activeDialog);
  });

  var confirmDialog = document.querySelector('[data-cycle-dialog="confirm"]');
  if (confirmDialog) {
    var confirmText = confirmDialog.querySelector('[data-confirm-text]');
    var confirmOk = confirmDialog.querySelector('[data-confirm-ok]');
    var pendingForm = null;

    document.querySelectorAll('form[data-cycle-confirm]').forEach(function (form) {
      form.addEventListener('submit', function (event) {
        if (form.dataset.confirmed === 'yes') return;
        event.preventDefault();
        pendingForm = form;
        confirmText.textContent = form.getAttribute('data-cycle-confirm');
        openDialog('confirm', form.querySelector('button'));
      });
    });

    confirmOk.addEventListener('click', function () {
      if (!pendingForm) return;
      pendingForm.dataset.confirmed = 'yes';
      closeDialog(confirmDialog);
      if (pendingForm.requestSubmit) pendingForm.requestSubmit();
      else pendingForm.submit();
      pendingForm = null;
    });
  }

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

  function renderAgenda(day) {
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

      if (day.record.summary) card.append(element('p', 'cycle-agenda-summary', day.record.summary));
      if (day.record.note) card.append(element('blockquote', null, day.record.note));
      agendaBody.append(card);
    }

    if (day.log) {
      var log = element('div', 'cycle-agenda-log');
      var facts = element('dl', 'cycle-agenda-facts');
      if (day.log.flow) facts.append(factRow('经量', day.log.flowName));
      if (day.log.mood) facts.append(factRow('心情', day.log.moodName));
      if (day.log.pain) facts.append(factRow('不适', day.log.painName));
      if (day.log.symptomNames.length) facts.append(factRow('身体状况', day.log.symptomNames.join('、')));
      if (facts.childElementCount) log.append(facts);
      if (day.log.note) log.append(element('p', 'cycle-agenda-note', day.log.note));
      log.append(element('small', null, '由 ' + day.log.updatedBy + ' 记录'));
      agendaBody.append(log);
    } else if (!day.isFuture) {
      agendaBody.append(element('p', 'cycle-agenda-empty', '这一天暂时没有补充记录，双方都可以继续填写。'));
    }

    agendaEdit.hidden = day.isFuture;
  }

  /* ------------------------------------------------------------ 月历格子 */

  function renderDay(day) {
    var button = element('button', 'calendar-day phase-' + day.phase);
    button.type = 'button';
    button.setAttribute('role', 'gridcell');
    button.dataset.date = day.date;
    button.setAttribute('aria-label', fullDate(day.date) + ' ' + day.phaseName);
    button.setAttribute('aria-selected', 'false');
    if (!day.inMonth) button.classList.add('is-outside');
    if (day.isToday) button.classList.add('is-today');
    if (day.isFuture) button.classList.add('is-future');
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
    date.append(element('span', 'calendar-day-number', day.day));
    if (label) date.append(element('small', 'calendar-day-secondary', label));
    button.append(date);

    var marks = element('span', 'cycle-day-marks');
    marks.setAttribute('aria-hidden', 'true');
    if (day.periodDay) marks.append(element('i', 'is-period'));
    if (day.log) marks.append(element('i', 'is-log'));
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

  var editor = document.querySelector('[data-cycle-dialog="day-editor"]');

  function check(form, name, value) {
    var input = form.querySelector('input[name="' + name + '"][value="' + value + '"]');
    if (input) input.checked = true;
  }

  function openDayEditor(day) {
    if (!editor) return;
    var form = editor.querySelector('form');
    form.reset();

    form.querySelector('[data-day-date]').value = day.date;
    editor.querySelector('[data-day-title]').textContent = fullDate(day.date);
    editor.querySelector('[data-day-hint]').textContent = day.periodDay
      ? '经期第 ' + day.periodDay + ' 天 · ' + day.phaseName
      : day.phaseName + ' · ' + day.phaseHint;

    if (day.log) {
      check(form, 'flow', day.log.flow);
      check(form, 'mood', day.log.mood);
      check(form, 'pain', day.log.pain);
      form.querySelectorAll('input[name="symptoms"]').forEach(function (box) {
        box.checked = (day.log.symptoms & Number(box.value)) !== 0;
      });
      form.querySelector('textarea[name="note"]').value = day.log.note;
    }

    openDialog('day-editor', agendaEdit);
  }

  agendaEdit.addEventListener('click', function () {
    if (selected) openDayEditor(selected);
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
