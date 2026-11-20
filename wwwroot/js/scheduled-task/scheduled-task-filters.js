let filterTimeout;
document.addEventListener('multiselect:change', (e) => {
    // e.detail.id -> 'task-filter-types' / 'task-filter-statuses' / 'task-filter-crons'
    // e.detail.values -> массив выбранных строк (например ['0', '3']) или [] если выбран All
    filterValues.Type = window.getMultiSelectValues('task-filter-types');
    filterValues.Status = window.getMultiSelectValues('task-filter-statuses');
    filterValues.Cron = window.getMultiSelectValues('task-filter-crons');

    clearTimeout(filterTimeout);
    filterTimeout = setTimeout(reload, 500);
});
