document.getElementById('task-filter-types').onchange = (e) => {
    filterValues.Type = e.target.value;
    reload();
};
document.getElementById('task-filter-statuses').onchange = (e) => {
    filterValues.Status = e.target.value;
    reload();
};
document.getElementById('task-filter-crons').onchange = (e) => {
    filterValues.Cron = e.target.value;
    reload();
};
