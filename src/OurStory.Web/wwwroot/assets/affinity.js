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
