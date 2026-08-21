document.querySelectorAll('[data-answer-form]').forEach(function (form) {
    var choices = Array.from(form.querySelectorAll('input[name="optionIndexes"]'));
    var textAnswer = form.querySelector('textarea[name="textAnswer"]');
    var fieldset = form.querySelector('fieldset');
    var error = form.querySelector('[data-answer-error]');

    function hasAnswer() {
        return choices.some(function (choice) { return choice.checked; })
            || Boolean(textAnswer && textAnswer.value.trim());
    }

    function clearError() {
        if (!hasAnswer()) return;
        fieldset && fieldset.classList.remove('has-error');
        textAnswer && textAnswer.classList.remove('has-error');
        if (error) error.hidden = true;
    }

    choices.forEach(function (choice) { choice.addEventListener('change', clearError); });
    textAnswer && textAnswer.addEventListener('input', clearError);
    form.addEventListener('submit', function (event) {
        if (hasAnswer()) return;
        event.preventDefault();
        fieldset && fieldset.classList.add('has-error');
        textAnswer && textAnswer.classList.add('has-error');
        if (error) error.hidden = false;
        if (textAnswer) textAnswer.focus();
        else choices[0] && choices[0].focus();
    });
});

document.querySelectorAll('.reveal-answers strong, .history-answer > p').forEach(function (answer, index) {
    answer.classList.add('answer-copy');

    var collapseHeight = parseFloat(getComputedStyle(answer).getPropertyValue('--answer-collapse-height'));
    if (!collapseHeight || answer.scrollHeight <= collapseHeight + 1) return;

    var answerId = answer.id || 'affinity-answer-' + index;
    answer.id = answerId;
    answer.classList.add('is-collapsible', 'is-collapsed');

    var button = document.createElement('button');
    button.type = 'button';
    button.className = 'answer-expand';
    button.setAttribute('aria-controls', answerId);
    button.setAttribute('aria-expanded', 'false');
    button.textContent = '展开全部 ↓';
    answer.insertAdjacentElement('afterend', button);

    button.addEventListener('click', function () {
        var collapsed = answer.classList.toggle('is-collapsed');
        button.setAttribute('aria-expanded', String(!collapsed));
        button.textContent = collapsed ? '展开全部 ↓' : '收起 ↑';
    });
});
