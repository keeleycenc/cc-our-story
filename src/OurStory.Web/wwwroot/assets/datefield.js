(function () {
  'use strict';

  var pad = function (value) { return String(value).padStart(2, '0'); };

  var localStamp = function (withTime) {
    var now = new Date();
    var day = now.getFullYear() + '-' + pad(now.getMonth() + 1) + '-' + pad(now.getDate());
    return withTime ? day + 'T' + pad(now.getHours()) + ':' + pad(now.getMinutes()) : day;
  };

  function bind(root) {
    (root || document).querySelectorAll('[data-date-field]').forEach(function (field) {
      if (field.dataset.dateReady === '1') return;
      var input = field.querySelector('[data-date-input]');
      if (!input) return;
      field.dataset.dateReady = '1';

      var open = field.querySelector('[data-date-open]');
      var now = field.querySelector('[data-date-now]');

      if (open && typeof input.showPicker === 'function') {
        field.classList.add('has-picker');
        open.hidden = false;
        open.addEventListener('click', function () {
          try { input.showPicker(); } catch (error) { input.focus(); }
        });
      }

      if (now) {
        now.hidden = false;
        now.addEventListener('click', function () {
          var stamp = localStamp(input.type === 'datetime-local');
          if (input.max && stamp > input.max) stamp = input.max;
          input.value = stamp;
          input.dispatchEvent(new Event('input', { bubbles: true }));
          input.dispatchEvent(new Event('change', { bubbles: true }));
        });
      }
    });
  }

  window.bindDateFields = bind;

  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', function () { bind(document); });
  } else {
    bind(document);
  }
}());
