(function () {
  'use strict';

  const helpButton = document.querySelector('[data-help-open]');
  const helpPanel = document.getElementById('shop-help');
  if (helpButton && helpPanel) {
    let shell = null;

    const closeHelp = () => {
      if (!shell) return;
      shell.classList.remove('is-open');
      document.body.classList.remove('modal-open');
      helpButton.focus();
    };

    helpButton.addEventListener('click', () => {
      if (!shell) {
        shell = document.createElement('div');
        shell.className = 'shop-dialog';
        shell.innerHTML =
          '<div class="shop-dialog-mask" data-help-close></div>' +
          '<div class="shop-dialog-card is-help" role="dialog" aria-modal="true"' +
          ' aria-labelledby="shop-help-title"></div>';

        const card = shell.querySelector('.shop-dialog-card');
        card.appendChild(helpPanel);

        const actions = document.createElement('div');
        actions.className = 'shop-dialog-actions';
        actions.innerHTML = '<button class="shop-buy" type="button" data-help-ok>知道啦</button>';
        card.appendChild(actions);

        document.body.appendChild(shell);
        shell.querySelector('[data-help-close]').addEventListener('click', closeHelp);
        shell.querySelector('[data-help-ok]').addEventListener('click', closeHelp);
        document.addEventListener('keydown', (event) => {
          if (event.key === 'Escape') closeHelp();
        });
      }

      shell.classList.add('is-open');
      document.body.classList.add('modal-open');
      shell.querySelector('[data-help-ok]').focus();
    });
  }

  const forms = document.querySelectorAll('form[data-confirm], form[data-confirm-title]');
  if (forms.length === 0) return;

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
    document.body.classList.remove('modal-open');

    const done = settle;
    settle = null;
    if (opener && document.contains(opener)) opener.focus();
    opener = null;
    if (done) done(answer);
  };

  const build = () => {
    host = document.createElement('div');
    host.className = 'shop-dialog';
    host.innerHTML =
      '<div class="shop-dialog-mask" data-dialog-cancel></div>' +
      '<div class="shop-dialog-card" role="alertdialog" aria-modal="true"' +
      ' aria-labelledby="shop-dialog-title" aria-describedby="shop-dialog-text">' +
      '<span class="shop-dialog-icon"><svg class="icon" aria-hidden="true"><use href="#i-hand-heart"></use></svg></span>' +
      '<h2 id="shop-dialog-title"></h2>' +
      '<p id="shop-dialog-text"></p>' +
      '<div class="shop-dialog-actions">' +
      '<button class="shop-ghost" type="button" data-dialog-cancel>再想想</button>' +
      '<button class="shop-buy" type="button" data-dialog-ok></button>' +
      '</div></div>';

    document.body.appendChild(host);

    titleNode = host.querySelector('#shop-dialog-title');
    textNode = host.querySelector('#shop-dialog-text');
    okButton = host.querySelector('[data-dialog-ok]');
    cancelButton = host.querySelector('.shop-dialog-actions [data-dialog-cancel]');
    iconUse = host.querySelector('.shop-dialog-icon use');

    host.querySelectorAll('[data-dialog-cancel]').forEach((node) => {
      node.addEventListener('click', () => close(false));
    });
    okButton.addEventListener('click', () => close(true));
    document.addEventListener('keydown', (event) => {
      if (event.key === 'Escape') close(false);
    });
  };

  const ask = (options) => new Promise((done) => {
    if (!host) build();

    close(false);

    titleNode.textContent = options.title || '确定要这么做吗？';
    textNode.textContent = options.text || '';
    textNode.hidden = !options.text;
    okButton.textContent = options.ok || '确定';
    cancelButton.textContent = '再想想';
    if (iconUse) iconUse.setAttribute('href', '#i-' + (options.icon || 'hand-heart'));

    settle = done;
    opener = document.activeElement;
    host.classList.add('is-open');
    document.body.classList.add('modal-open');
    okButton.focus();
  });

  forms.forEach((form) => {
    form.addEventListener('submit', (event) => {
      if (form.dataset.confirmed === 'yes') return;

      event.preventDefault();
      ask({
        title: form.dataset.confirmTitle,
        text: form.dataset.confirm,
        ok: form.dataset.confirmOk
      }).then((yes) => {
        if (!yes) return;

        form.dataset.confirmed = 'yes';
        const submit = form.querySelector('button[type="submit"]');
        if (submit) submit.disabled = true;
        if (form.requestSubmit) form.requestSubmit();
        else form.submit();
      });
    });
  });
}());
