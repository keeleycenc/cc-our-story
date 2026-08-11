(function () {
  'use strict';

  /* 配色切换：和前台共用 localStorage 里的那一项，两边保持一致
     手机顶栏和 PC 顶栏各有一个开关，两个都要接上 */
  const root = document.documentElement;
  document.querySelectorAll('[data-theme-toggle]').forEach((toggle) => {
    toggle.addEventListener('click', () => {
      const next = root.dataset.theme === 'dark' ? 'light' : 'dark';
      root.dataset.theme = next;
      try { localStorage.setItem('cc-color-mode', next); } catch (e) {}
    });
  });

  /* 手机底栏的「更多」：把不常用的入口收进一张从底部推上来的卡片 */
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

  const confirmDialog = (function () {
    let host = null;
    let titleNode = null;
    let textNode = null;
    let okButton = null;
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

  /* 编辑器里的插图：上传完直接把 Markdown 图片语法插到光标处 */
  const uploadInput = document.querySelector('[data-upload-input]');
  const editor = document.querySelector('[data-editor]');
  const status = document.querySelector('[data-upload-status]');

  if (uploadInput && editor) {
    uploadInput.addEventListener('change', async () => {
      const file = uploadInput.files && uploadInput.files[0];
      if (!file) return;

      const body = new FormData();
      body.append('file', file);
      const token = document.querySelector('input[name="__RequestVerificationToken"]');
      if (token) body.append('__RequestVerificationToken', token.value);

      if (status) status.textContent = '正在上传…';

      try {
        const response = await fetch('/admin/media?handler=Upload', { method: 'POST', body: body });
        const data = await response.json();

        if (!response.ok || !data.ok) {
          if (status) status.textContent = data.error || '上传失败。';
          return;
        }

        const snippet = '\n![](' + data.url + ')\n';
        const at = editor.selectionStart || editor.value.length;
        editor.value = editor.value.slice(0, at) + snippet + editor.value.slice(at);
        editor.focus();
        editor.selectionStart = editor.selectionEnd = at + snippet.length;
        if (status) status.textContent = '已插入正文。';
      } catch (error) {
        if (status) status.textContent = '网络开了个小差，稍后再试。';
      } finally {
        uploadInput.value = '';
      }
    });
  }
}());
