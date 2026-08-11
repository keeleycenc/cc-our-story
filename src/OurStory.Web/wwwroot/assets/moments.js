(function () {
  'use strict';

/* 图片查看器 */
  const lightbox = document.getElementById('lightbox');
  if (lightbox) {
    const image = lightbox.querySelector('img');
    const closeLightbox = () => {
      lightbox.hidden = true;
      document.body.classList.remove('modal-open');
      image.removeAttribute('src');
    };
    document.querySelectorAll('.article-body img').forEach((img) => {
      img.classList.add('is-zoomable');
      img.addEventListener('click', () => {
        image.src = img.currentSrc || img.src;
        image.alt = img.alt || '';
        lightbox.hidden = false;
        document.body.classList.add('modal-open');
        lightbox.querySelector('.lightbox-close').focus();
      });
    });
    lightbox.addEventListener('click', closeLightbox);
    document.addEventListener('keydown', (event) => {
      if (event.key === 'Escape' && !lightbox.hidden) closeLightbox();
    });
  }

/* 留言：回复框跟着被回复的那条走，取消后回到最下面 */
  const respond = document.getElementById('respond');
  if (respond) {
    const parentInput = respond.querySelector('input[name="replyTo"]');
    const title = respond.querySelector('[data-respond-title]');
    const cancel = respond.querySelector('.cancel-comment-reply');
    const textarea = respond.querySelector('textarea');
    const defaultTitle = title ? title.textContent : '';
    // 表单原来待的地方，取消回复时按它放回去
    const home = document.createComment('respond');
    respond.parentNode.insertBefore(home, respond);

    const reset = () => {
      home.parentNode.insertBefore(respond, home.nextSibling);
      respond.classList.remove('is-inline');
      if (parentInput) parentInput.value = '0';
      if (title) title.textContent = defaultTitle;
      if (cancel) cancel.hidden = true;
    };

    const replyTo = (item, name) => {
      if (respond.parentNode === item) {
        reset();
        return;
      }
      item.appendChild(respond);
      respond.classList.add('is-inline');
      if (parentInput) parentInput.value = item.id.replace('comment-', '');
      if (title) title.textContent = '回复 @' + name;
      if (cancel) cancel.hidden = false;
    };

    document.querySelectorAll('[data-reply-to]').forEach((link) => {
      link.addEventListener('click', (event) => {
        const item = link.closest('.comment');
        if (!item) return;
        event.preventDefault();
        replyTo(item, link.dataset.replyName || '');
        if (textarea) textarea.focus({ preventScroll: true });
        respond.scrollIntoView({ block: 'nearest' });
      });
    });

    if (cancel) {
      cancel.addEventListener('click', (event) => {
        event.preventDefault();
        reset();
      });
    }

    // 没有 JS 时点「回复」是带着 ?replyTo= 重新加载的，这里把表单接着搬到位
    if (parentInput && parentInput.value !== '0') {
      const target = document.getElementById('comment-' + parentInput.value);
      if (target) {
        const name = target.querySelector('.comment-name');
        replyTo(target, name ? name.textContent.trim() : '');
      } else {
        // 要回的那条已经不在了，当成写新留言，别提交一个挂空的 parentId
        reset();
      }
    }
  }

/* 留言：复制这一条的直链 */
  document.querySelectorAll('[data-comment-link]').forEach((link) => {
    const label = link.querySelector('span');
    if (!label) return;
    const text = label.textContent;

    link.addEventListener('click', (event) => {
      if (!navigator.clipboard) return;
      event.preventDefault();
      const url = new URL(link.getAttribute('href'), window.location.href).href;
      navigator.clipboard.writeText(url).then(() => {
        label.textContent = '已复制';
        link.classList.add('is-copied');
        window.setTimeout(() => {
          label.textContent = text;
          link.classList.remove('is-copied');
        }, 1600);
      }, () => {
        // 剪贴板被浏览器挡了就退回普通锚点
        window.location.hash = link.getAttribute('href');
      });
    });
  });

/* 留言：直链指向的那条被折叠了的话，先展开再滚过去 */
  const revealComment = () => {
    if (!window.location.hash.startsWith('#comment-')) return;
    const target = document.getElementById(window.location.hash.slice(1));
    if (!target) return;

    let opened = false;
    for (let box = target.closest('details'); box; box = box.parentElement.closest('details')) {
      if (!box.open) {
        box.open = true;
        opened = true;
      }
    }
    if (opened) target.scrollIntoView({ block: 'center' });
  };

  revealComment();
  window.addEventListener('hashchange', revealComment);

/* 受密码保护的文章：就地校验，不跳转到异常页 */
  const protectedForm = document.querySelector('.article-body form.protected');
  if (protectedForm) {
    const password = protectedForm.querySelector('input[name="protectPassword"]');
    const submit = protectedForm.querySelector('input[type="submit"]');
    if (password) password.setAttribute('placeholder', '输入这篇记录的访问密码');
    if (submit) submit.value = '解锁这篇记录';

    protectedForm.addEventListener('submit', async (event) => {
      event.preventDefault();
      if (!password || !password.value) {
        window.ccShowModal({ title: '还没有输入密码', message: '输入密码后再试一次吧。', eyebrow: 'PRIVATE MOMENT', icon: 'lock' });
        return;
      }

      if (submit) {
        submit.disabled = true;
        submit.value = '验证中…';
      }

      try {
        const response = await fetch(protectedForm.action, {
          method: 'POST',
          body: new FormData(protectedForm),
          credentials: 'same-origin'
        });

        if (response.ok) {
          window.location.reload();
          return;
        }

        const html = await response.text();
        const page = new DOMParser().parseFromString(html, 'text/html');
        const detail = page.querySelector('[data-exception-message], .container');
        window.ccShowModal({
          title: '密码不正确',
          message: detail ? detail.textContent.trim() : '请检查密码后重新输入。',
          eyebrow: 'PRIVATE MOMENT',
          icon: 'circle-alert',
          button: '重新输入'
        });
        password.focus();
      } catch (error) {
        window.ccShowModal({ title: '暂时无法验证', message: '网络似乎开了个小差，请稍后再试。', eyebrow: 'OUR STORY', icon: 'wifi-off' });
      } finally {
        if (submit) {
          submit.disabled = false;
          submit.value = '解锁这篇记录';
        }
      }
    });
  }
}());
