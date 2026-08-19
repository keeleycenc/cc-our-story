(function () {
  'use strict';

  /* 配色切换：和前台共用 localStorage 里的那一项，两边保持一致
     手机顶栏和 PC 页头各有一个开关，两个都要接上 */
  const root = document.documentElement;
  document.querySelectorAll('[data-theme-toggle]').forEach((toggle) => {
    toggle.addEventListener('click', () => {
      const next = root.dataset.theme === 'dark' ? 'light' : 'dark';
      root.dataset.theme = next;
      try { localStorage.setItem('cc-color-mode', next); } catch (e) {}
    });
  });

  /* 手机顶栏的「更多」：整份导航收在一张从底部推上来的卡片里 */
  const sheet = document.querySelector('[data-sheet]');
  if (sheet) {
    const openers = document.querySelectorAll('[data-sheet-open]');

    const setSheet = (open) => {
      sheet.classList.toggle('is-open', open);
      document.body.classList.toggle('sheet-open', open);
      openers.forEach((button) => button.setAttribute('aria-expanded', String(open)));
    };

    openers.forEach((button) => button.addEventListener('click', () => setSheet(!sheet.classList.contains('is-open'))));
    sheet.querySelectorAll('[data-sheet-close]').forEach((button) => button.addEventListener('click', () => setSheet(false)));

    document.addEventListener('keydown', (event) => {
      if (event.key === 'Escape' && sheet.classList.contains('is-open')) setSheet(false);
    });
  }

  /* 回到顶部：后台滚的是中间那块内容区，不是整个页面，
     所以监听和滚动都得冲着 .admin-main 去，window.scrollTo 在这儿没用 */
  const canvas = document.querySelector('.admin-main');
  const toTop = document.querySelector('.admin-to-top');
  if (canvas && toTop) {
    const onScroll = () => { toTop.hidden = canvas.scrollTop < 400; };

    onScroll();
    canvas.addEventListener('scroll', onScroll, { passive: true });

    toTop.addEventListener('click', () => {
      const still = window.matchMedia('(prefers-reduced-motion: reduce)').matches;
      canvas.scrollTo({ top: 0, behavior: still ? 'auto' : 'smooth' });
    });
  }

  const confirmDialog = (function () {
    let host = null;
    let titleNode = null;
    let textNode = null;
    let okButton = null;
    let cancelButton = null;
    let iconUse = null;
    let settle = null;
    let opener = null;

    const close = (answer) => {
      if (!host || !host.classList.contains('is-open')) return;
      host.classList.remove('is-open');
      document.body.classList.remove('sheet-open');

      const done = settle;
      settle = null;
      if (opener && document.contains(opener)) opener.focus();
      opener = null;
      if (done) done(answer);
    };

    const build = () => {
      host = document.createElement('div');
      host.className = 'admin-dialog';
      host.innerHTML =
        '<div class="admin-dialog-mask" data-dialog-cancel></div>' +
        '<div class="admin-dialog-card" role="alertdialog" aria-modal="true"' +
        ' aria-labelledby="admin-dialog-title" aria-describedby="admin-dialog-text">' +
        '<span class="admin-dialog-icon"><svg class="icon" aria-hidden="true"><use href="#i-trash-2"></use></svg></span>' +
        '<h2 class="admin-dialog-title" id="admin-dialog-title"></h2>' +
        '<p class="admin-dialog-text" id="admin-dialog-text"></p>' +
        '<div class="admin-dialog-actions">' +
        '<button class="btn btn-ghost" type="button" data-dialog-cancel>再想想</button>' +
        '<button class="btn btn-solid-danger" type="button" data-dialog-ok></button>' +
        '</div></div>';

      document.body.appendChild(host);

      titleNode = host.querySelector('.admin-dialog-title');
      textNode = host.querySelector('.admin-dialog-text');
      okButton = host.querySelector('[data-dialog-ok]');
      cancelButton = host.querySelector('.admin-dialog-actions [data-dialog-cancel]');
      iconUse = host.querySelector('.admin-dialog-icon use');

      host.querySelectorAll('[data-dialog-cancel]').forEach((node) => {
        node.addEventListener('click', () => close(false));
      });
      okButton.addEventListener('click', () => close(true));

      document.addEventListener('keydown', (event) => {
        if (event.key === 'Escape') close(false);
      });
    };

    return (options) => new Promise((done) => {
      if (!host) build();

      // 上一张还开着就当作放弃，避免两次询问叠在一起
      close(false);

      titleNode.textContent = options.title || '确定要删掉吗？';
      textNode.textContent = options.text || '';
      textNode.hidden = !options.text;
      okButton.textContent = options.ok || '删掉';
      cancelButton.textContent = options.cancel || '再想想';
      cancelButton.hidden = options.notice === true;
      iconUse.setAttribute('href', '#i-' + (options.icon || 'trash-2'));
      host.classList.toggle('is-warning', options.tone === 'warning');
      okButton.className = options.danger === false ? 'btn' : 'btn btn-solid-danger';

      settle = done;
      opener = document.activeElement;
      host.classList.add('is-open');
      document.body.classList.add('sheet-open');
      okButton.focus();
    });
  }());

  document.querySelectorAll('form[data-confirm], form[data-confirm-title]').forEach((form) => {
    form.addEventListener('submit', (event) => {
      if (form.dataset.confirmed === 'yes') return;

      event.preventDefault();
      confirmDialog({
        title: form.dataset.confirmTitle,
        text: form.dataset.confirm,
        ok: form.dataset.confirmOk
      }).then((yes) => {
        if (!yes) return;
        form.dataset.confirmed = 'yes';
        if (form.requestSubmit) form.requestSubmit();
        else form.submit();
      });
    });
  });

  /* 编辑页离开保护：站内跳转统一使用自己的确认框，不再调用浏览器原生提示。 */
  document.querySelectorAll('form[data-dirty-guard]').forEach((form) => {
    let dirty = false;
    let submitting = false;

    const markDirty = () => { dirty = true; };
    form.addEventListener('input', markDirty);
    form.addEventListener('change', markDirty);
    form.addEventListener('submit', () => {
      submitting = true;
      dirty = false;
    });

    document.addEventListener('click', (event) => {
      if (!dirty || submitting || event.defaultPrevented || event.button !== 0 || event.metaKey || event.ctrlKey || event.shiftKey || event.altKey) return;

      const link = event.target.closest('a[href]');
      if (!link || link.target === '_blank' || link.hasAttribute('download')) return;

      const destination = new URL(link.href, window.location.href);
      if (destination.pathname === window.location.pathname && destination.search === window.location.search && destination.hash) return;

      event.preventDefault();
      confirmDialog({
        title: '更改尚未保存',
        text: '现在离开后，本页刚刚做的修改将不会保存。',
        ok: '不保存并离开',
        cancel: '继续编辑',
        icon: 'circle-alert',
        tone: 'warning',
        danger: false
      }).then((yes) => {
        if (!yes) return;
        dirty = false;
        window.location.assign(destination.href);
      });
    });
  });

  /* 留言正文折起来：CSS 里先按四行截断，这里只负责判断有没有被截到 ——
     没截到就不摆按钮，免得短短一句话下面还挂个没用的「展开」 */
  document.querySelectorAll('[data-clamp]').forEach((text) => {
    const toggle = text.parentNode.querySelector('[data-clamp-toggle]');
    if (!toggle) return;

    if (text.scrollHeight <= text.clientHeight + 2) return;

    const label = toggle.querySelector('span') || toggle;
    toggle.hidden = false;

    toggle.addEventListener('click', () => {
      const open = text.classList.toggle('is-open');
      toggle.classList.toggle('is-open', open);
      toggle.setAttribute('aria-expanded', String(open));
      label.textContent = open ? '收起' : '展开';
    });

    toggle.setAttribute('aria-expanded', 'false');
  });

  /* 图片占位：OSS 和外链有时要等上好几秒，先摆一块骨架屏把位置占住，
     图到了再淡入；实在拿不到就换成一句提示，不留一块空白 */
  document.querySelectorAll('[data-thumb]').forEach((thumb) => {
    const image = thumb.querySelector('img');
    if (!image) return;

    const mark = (state) => thumb.classList.add(state);

    // 脚本跑起来时图可能已经在缓存里了，这种情况下 load 不会再触发
    if (image.complete) {
      mark(image.naturalWidth > 0 ? 'is-ready' : 'is-failed');
      return;
    }

    image.addEventListener('load', () => mark('is-ready'));
    image.addEventListener('error', () => mark('is-failed'));
  });

  /* 图片库里的「复制链接」：文件名那一行放不下完整地址，靠这个按钮取 */
  document.querySelectorAll('[data-copy]').forEach((button) => {
    const label = button.querySelector('span') || button;
    const original = label.textContent;

    button.addEventListener('click', async () => {
      const text = button.dataset.copy;
      let ok = true;

      // 局域网里常常是 http，没有 clipboard API，退回到老办法
      if (navigator.clipboard && window.isSecureContext) {
        try { await navigator.clipboard.writeText(text); } catch (e) { ok = false; }
      } else {
        const holder = document.createElement('textarea');
        holder.value = text;
        holder.setAttribute('readonly', '');
        holder.style.position = 'fixed';
        holder.style.opacity = '0';
        document.body.appendChild(holder);
        holder.select();
        try { ok = document.execCommand('copy'); } catch (e) { ok = false; }
        document.body.removeChild(holder);
      }

      label.textContent = ok ? '已复制' : '复制不了，手动选一下';
      setTimeout(() => { label.textContent = original; }, 1600);
    });
  });

  /* 日期／时间选择器：值和弹出的那张日历仍旧交给原生控件，
     这里只补两件浏览器没做好的事 —— 一个自己画的日历按钮，
     和一个把日期跳到今天的快捷。两个按钮都是先藏着、确认能用了才亮出来 */
  const pad = (value) => String(value).padStart(2, '0');

  const localStamp = (withTime) => {
    const now = new Date();
    const day = now.getFullYear() + '-' + pad(now.getMonth() + 1) + '-' + pad(now.getDate());
    return withTime ? day + 'T' + pad(now.getHours()) + ':' + pad(now.getMinutes()) : day;
  };

  document.querySelectorAll('[data-date-field]').forEach((field) => {
    const input = field.querySelector('[data-date-input]');
    if (!input) return;

    const open = field.querySelector('[data-date-open]');
    const now = field.querySelector('[data-date-now]');

    // showPicker 不是哪儿都有；调不动就别摆按钮，把浏览器自带的日历图标留着
    if (open && typeof input.showPicker === 'function') {
      field.classList.add('has-picker');
      open.hidden = false;
      open.addEventListener('click', () => {
        try { input.showPicker(); } catch (error) { input.focus(); }
      });
    }

    if (now) {
      now.hidden = false;
      now.addEventListener('click', () => {
        input.value = localStamp(input.type === 'datetime-local');
        // 离开保护和其它监听都盯着这两个事件，改完得说一声
        input.dispatchEvent(new Event('input', { bubbles: true }));
        input.dispatchEvent(new Event('change', { bubbles: true }));
      });
    }
  });

  document.querySelectorAll('[data-anniversary-calendar-editor]').forEach((editor) => {
    const radios = Array.from(editor.querySelectorAll('input[name="Input.CalendarType"]'));
    const solar = editor.querySelector('[data-solar-date]');
    const lunar = editor.querySelector('[data-lunar-date]');
    const year = editor.querySelector('[data-lunar-year]');
    const month = editor.querySelector('[data-lunar-month]');
    const day = editor.querySelector('[data-lunar-day]');
    if (!radios.length || !solar || !lunar || !year || !month || !day) return;

    const setMode = () => {
      const selected = radios.find((radio) => radio.checked);
      const isLunar = selected && selected.value === 'Lunar';
      solar.hidden = isLunar;
      lunar.hidden = !isLunar;
    };

    const fillDays = () => {
      const chosen = month.options[month.selectedIndex];
      const count = Number(chosen ? chosen.dataset.days : 30) || 30;
      const selected = Math.min(Number(day.value || 1), count);
      Array.from(day.options).forEach((option) => { option.hidden = Number(option.value) > count; });
      day.value = String(selected);
    };

    const loadYear = async () => {
      const previous = month.value;
      const url = new URL(editor.dataset.lunarYearUrl, window.location.origin);
      url.searchParams.set('year', year.value);
      try {
        const response = await fetch(url, { headers: { Accept: 'application/json' } });
        if (!response.ok) throw new Error('HTTP ' + response.status);
        const payload = await response.json();
        month.replaceChildren(...payload.months.map((item) => {
          const option = document.createElement('option');
          option.value = item.value;
          option.textContent = item.label;
          option.dataset.days = String(item.days);
          return option;
        }));
        month.value = Array.from(month.options).some((option) => option.value === previous) ? previous : '1';
        fillDays();
      } catch (_) {
        // 保留上一组月份；服务端保存时仍会再次校验，不让错误日期进入数据库。
      }
    };

    radios.forEach((radio) => radio.addEventListener('change', setMode));
    year.addEventListener('change', loadYear);
    month.addEventListener('change', fillDays);
    setMode();
    fillDays();
  });

  /* 上传：图片库和正文插图共用这一套。
     一次能选好几张，但仍旧一张一张地传 —— 服务端一次只收一张，
     而且这样进度是准的，坏了也知道是坏在第几张 */
  const antiforgery = () => document.querySelector('input[name="__RequestVerificationToken"]');

  const uploadOne = (file, onProgress) => new Promise((resolve) => {
    const request = new XMLHttpRequest();
    const body = new FormData();
    const verification = antiforgery();

    body.append('file', file);
    if (verification) body.append('__RequestVerificationToken', verification.value);

    request.open('POST', '/admin/media?handler=Upload');
    request.responseType = 'json';

    // 传大图时这个事件一直在报进度，进度条走的就是它
    if (request.upload) {
      request.upload.addEventListener('progress', (event) => {
        if (event.lengthComputable) onProgress(event.loaded / event.total);
      });
    }

    request.addEventListener('load', () => {
      const data = request.response || {};
      resolve(data.ok ? { ok: true, url: data.url } : { ok: false, error: data.error || '上传失败。' });
    });

    request.addEventListener('error', () => resolve({ ok: false, error: '网络开了个小差，稍后再试。' }));
    request.send(body);
  });

  /* 进度条：整批算一个百分比，第几张单独用文字说 */
  const progressBoard = (host) => {
    const bar = host && host.querySelector('[data-upload-bar]');
    const text = host && host.querySelector('[data-upload-text]');

    return {
      start() { if (host) host.hidden = false; },
      set(ratio, label) {
        if (bar) bar.style.width = Math.round(Math.min(1, Math.max(0, ratio)) * 100) + '%';
        if (text) text.textContent = label;
      },
      stop(label) {
        if (bar) bar.style.width = '100%';
        if (text) text.textContent = label;
        if (!host) return;
        // 传完了把条子留一小会儿，让人看见它确实走到了头
        setTimeout(() => { host.hidden = true; if (bar) bar.style.width = '0%'; }, 1200);
      }
    };
  };

  /* 挨个上传，每传完一张就回调一次，最后给出这一批的结果 */
  const uploadAll = async (files, host, onDone) => {
    const board = progressBoard(host);
    const total = files.length;
    let uploaded = 0;
    let failure = null;

    board.start();

    for (let index = 0; index < total; index++) {
      const step = (fraction) => board.set(
        (index + fraction) / total,
        total > 1 ? '正在上传第 ' + (index + 1) + ' / ' + total + ' 张…' : '正在上传…'
      );

      step(0);
      const result = await uploadOne(files[index], step);

      if (result.ok) {
        uploaded++;
        if (onDone) onDone(result.url, files[index]);
      } else if (!failure) {
        failure = result.error;
      }
    }

    const summary = failure
      ? (uploaded > 0 ? '已上传 ' + uploaded + ' 张，其余上传失败：' + failure : failure)
      : (total > 1 ? uploaded + ' 已上传多张图片。' : '图片已上传。');

    board.stop(summary);
    return { uploaded: uploaded, failure: failure, summary: summary };
  };

  /* 选了哪几张：浏览器自带的那句「7 个文件」等于什么都没说。
     名字排成一行小标签，长名字交给 CSS 截断，多出来的收成「等 N 张」 */
  const sizeText = (bytes) => bytes < 1024 * 1024
    ? (bytes / 1024).toFixed(0) + ' KB'
    : (bytes / 1024 / 1024).toFixed(1) + ' MB';

  const describePick = (summary, files) => {
    if (!summary) return;

    summary.textContent = '';
    summary.hidden = files.length === 0;
    if (files.length === 0) return;

    const shown = files.slice(0, 3);
    const total = files.reduce((sum, file) => sum + file.size, 0);

    shown.forEach((file) => {
      const chip = document.createElement('li');
      chip.textContent = file.name;
      chip.title = file.name;
      summary.appendChild(chip);
    });

    const rest = document.createElement('li');
    rest.className = 'is-more';
    rest.textContent = files.length > shown.length
      ? '…等 ' + files.length + ' 张 · ' + sizeText(total)
      : '共 ' + sizeText(total);
    summary.appendChild(rest);
  };

  /* 图片库的上传表单：接管提交，改成带进度的逐张上传，传完再刷新列表 */
  document.querySelectorAll('form[data-media-upload]').forEach((form) => {
    const input = form.querySelector('[data-media-files]');
    const host = form.querySelector('[data-upload-progress]');
    const summary = form.querySelector('[data-file-summary]');
    const submit = form.querySelector('button[type="submit"]');
    if (!input) return;

    input.addEventListener('change', () => describePick(summary, Array.from(input.files || [])));

    form.addEventListener('submit', async (event) => {
      const files = Array.from(input.files || []);
      if (files.length === 0) return;

      event.preventDefault();
      if (submit) submit.disabled = true;

      const result = await uploadAll(files, host);

      input.value = '';
      describePick(summary, []);
      if (submit) submit.disabled = false;

      // 全传上去了就刷新，新图会排在「最近上传」的最前面；
      // 有失败的就停在这儿，把原因留在屏幕上
      if (!result.failure) setTimeout(() => window.location.reload(), 700);
    });
  });
 
  document.querySelectorAll('[data-cover-uploader]').forEach((picker) => {
    const target = picker.querySelector('[data-cover-target]');
    const input = picker.querySelector('[data-cover-upload]');
    const progressHost = picker.querySelector('[data-upload-progress]');
    const status = picker.querySelector('[data-cover-status]');
    if (!target || !input) return;

    input.addEventListener('change', async () => {
      const files = Array.from(input.files || []);
      if (files.length === 0) return;

      const result = await uploadAll(files.slice(0, 1), progressHost, (url) => {
        target.value = url;
        target.dispatchEvent(new Event('input', { bubbles: true }));
      });

      if (status) status.textContent = result.failure ? result.summary : '已设置封面';
      input.value = '';
    });
  });
 
  document.querySelectorAll('[data-shop-go], [data-shop-tip]').forEach((card) => {
    const go = card.dataset.shopGo;
    const tip = card.dataset.shopTip;
    if (!go && !tip) return;

    const done = card.classList.contains('is-done');

    const act = () => {
      if (go) { window.location.href = go; return; }
      confirmDialog({
        title: card.dataset.shopTipTitle || '温馨提示',
        text: tip,
        ok: '知道啦',
        notice: true,
        danger: false,
        icon: done ? 'circle-check' : 'clock'
      });
    };

    card.addEventListener('click', act);
    card.addEventListener('keydown', (event) => {
      if (event.key !== 'Enter' && event.key !== ' ') return;
      event.preventDefault();
      act();
    });
  });

  document.querySelectorAll('[data-shop-filter-form]').forEach((form) => {
    form.querySelectorAll('select').forEach((select) => {
      select.addEventListener('change', () => form.requestSubmit());
    });
  });

  document.querySelectorAll('[data-slider-field]').forEach((field) => {
    const slider = field.querySelector('[data-slider]');
    const output = field.querySelector('[data-slider-value]');
    if (!slider || !output) return;

    const unit = output.dataset.sliderUnit || '';
    const show = () => { output.textContent = slider.value + unit; };

    show();
    slider.addEventListener('input', show);
  });

  document.querySelectorAll('[data-shop-preset]').forEach((select) => {
    const form = select.closest('form');
    if (!form) return;

    const fields = [
      { node: form.querySelector('[data-shop-title]'), key: 'presetTitle', written: null },
      { node: form.querySelector('[data-shop-description]'), key: 'presetDescription', written: null },
      { node: form.querySelector('[data-cover-target]'), key: 'presetCover', written: null }
    ];

    select.addEventListener('change', () => {
      const option = select.options[select.selectedIndex];
      if (!option || !option.value) return;

      fields.forEach((field) => {
        if (!field.node) return;

        const current = field.node.value.trim();
        if (current.length > 0 && current !== field.written) return;

        field.written = (option.dataset[field.key] || '').trim();
        field.node.value = field.written;
      });

      const redeem = form.querySelector('input[name$="RedeemMode"][value="' + option.dataset.presetRedeem + '"]');
      if (redeem) redeem.checked = true;
    });
  });

  /* 点点滴滴和纪念日共用同一套 Markdown、预览和插图行为。 */
  document.querySelectorAll('[data-markdown-composer]').forEach((composer) => {
    const editor = composer.querySelector('[data-markdown-editor]');
    const editorPane = composer.querySelector('[data-markdown-editor-pane]');
    const previewPane = composer.querySelector('[data-markdown-preview-pane]');
    const editButton = composer.querySelector('[data-markdown-edit]');
    const previewButton = composer.querySelector('[data-markdown-preview-button]');
    const uploadInput = composer.querySelector('[data-markdown-upload]');
    const progressHost = composer.querySelector('[data-upload-progress]');
    const status = composer.querySelector('[data-markdown-status]');
    const cover = composer.querySelector('.markdown-cover-field input');
    let renderedValue = null;

    if (!editor || !editorPane || !previewPane) return;

    const setMode = (previewing) => {
      editorPane.hidden = previewing;
      previewPane.hidden = !previewing;
      editButton.classList.toggle('is-active', !previewing);
      previewButton.classList.toggle('is-active', previewing);
      editButton.setAttribute('aria-selected', String(!previewing));
      previewButton.setAttribute('aria-selected', String(previewing));
    };

    editButton.addEventListener('click', () => {
      setMode(false);
      editor.focus();
    });

    previewButton.addEventListener('click', async () => {
      if (renderedValue === editor.value) {
        setMode(true);
        return;
      }

      const body = new FormData();
      body.append('content', editor.value);
      const verification = antiforgery();
      if (verification) body.append('__RequestVerificationToken', verification.value);
      status.textContent = '正在生成预览…';

      try {
        const response = await fetch('/admin/media?handler=Preview', { method: 'POST', body: body });
        const data = await response.json();
        if (!response.ok || !data.ok) throw new Error('preview failed');

        previewPane.innerHTML = data.html || '<p class="markdown-preview-empty">填写内容后，在此预览最终排版</p>';
        renderedValue = editor.value;
        setMode(true);
        status.textContent = '预览已更新。';
      } catch (error) {
        status.textContent = '预览暂时生成不了，请稍后重试。';
      }
    });

    if (!uploadInput) return;

    let caretKnown = false;
    editor.addEventListener('focus', () => { caretKnown = true; });

    const insertAt = () => {
      const value = editor.value;
      if (!caretKnown || typeof editor.selectionEnd !== 'number') return value.length;

      const caret = Math.min(Math.max(editor.selectionEnd, 0), value.length);
      const line = value.indexOf('\n', caret);

      return line === -1 ? value.length : line;
    };

    const insert = (url) => {
      const at = insertAt();
      const before = editor.value.slice(0, at);
      const after = editor.value.slice(at);
      const head = before.length === 0 || before.endsWith('\n\n') ? '' : (before.endsWith('\n') ? '\n' : '\n\n');
      const tail = after.length === 0 || after.startsWith('\n\n') ? '' : (after.startsWith('\n') ? '\n' : '\n\n');
      const snippet = head + '![图片](' + url + ')' + tail;

      if (typeof editor.setRangeText === 'function') {
        editor.setRangeText(snippet, at, at, 'end');
      } else {
        editor.value = before + snippet + after;
        editor.selectionStart = editor.selectionEnd = at + snippet.length;
      }

      caretKnown = true;
      editor.dispatchEvent(new Event('input', { bubbles: true }));
      if (cover && !cover.value) cover.value = url;
    };

    uploadInput.addEventListener('change', async () => {
      const files = Array.from(uploadInput.files || []);
      if (files.length === 0) return;

      setMode(false);
      status.textContent = '';

      const result = await uploadAll(files, progressHost, insert);

      if (result.uploaded > 0) {
        renderedValue = null;
        editor.focus();
      }

      // 进度条那行说的是传得怎么样，这里说的是正文里发生了什么，两句话不重样
      status.textContent = result.failure
        ? result.summary
        : (result.uploaded > 1 ? result.uploaded + ' 张图片都插进正文了。' : '图片已插入正文。');

      uploadInput.value = '';
    });
  });
}());
