/* AcxiomCRM site scripts (vanilla JS — no jQuery dependency). */
(function () {
  'use strict';

  // Auto-dismiss TempData alerts after 4 seconds (Bootstrap alert API if present).
  document.querySelectorAll('.alert.auto-dismiss').forEach(function (el) {
    window.setTimeout(function () {
      if (window.bootstrap && window.bootstrap.Alert) {
        var alert = window.bootstrap.Alert.getOrCreateInstance(el);
        alert.close();
      } else {
        el.remove();
      }
    }, 4000);
  });

  // Delete confirmations: any element with data-confirm="<message>".
  // Works on <a> links and on submit buttons inside <form>s.
  document.addEventListener('click', function (e) {
    var el = e.target.closest('[data-confirm]');
    if (!el) return;
    var message = el.getAttribute('data-confirm') || 'Are you sure?';
    if (!window.confirm(message)) {
      e.preventDefault();
      e.stopPropagation();
    }
  }, true);

  // Mobile sidebar toggle.
  var toggle = document.getElementById('sidebarToggle');
  var backdrop = document.getElementById('sidebarBackdrop');
  if (toggle) {
    toggle.addEventListener('click', function () {
      document.body.classList.toggle('sidebar-open');
    });
  }
  if (backdrop) {
    backdrop.addEventListener('click', function () {
      document.body.classList.remove('sidebar-open');
    });
  }

  // Dashboard date presets: set the from/to date inputs, then submit.
  document.querySelectorAll('[data-range-preset]').forEach(function (btn) {
    btn.addEventListener('click', function () {
      var preset = btn.getAttribute('data-range-preset');
      var fromInput = document.getElementById('from');
      var toInput = document.getElementById('to');
      var form = document.getElementById('dateFilterForm');
      if (!fromInput || !toInput) return;

      var today = new Date();
      var fmt = function (d) {
        return d.getFullYear() + '-' +
          String(d.getMonth() + 1).padStart(2, '0') + '-' +
          String(d.getDate()).padStart(2, '0');
      };

      if (preset === 'today') {
        fromInput.value = fmt(today);
        toInput.value = fmt(today);
      } else if (preset === 'week') {
        var monday = new Date(today);
        var day = (today.getDay() + 6) % 7; // Monday = 0
        monday.setDate(today.getDate() - day);
        fromInput.value = fmt(monday);
        toInput.value = fmt(today);
      } else if (preset === 'month') {
        var first = new Date(today.getFullYear(), today.getMonth(), 1);
        fromInput.value = fmt(first);
        toInput.value = fmt(today);
      }

      if (form) form.submit();
    });
  });
})();
