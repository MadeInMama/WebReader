const typeSelect = document.getElementById('create-task-types');
const dateTimeInput = document.getElementById('create-task-have-to-start-at');
const priorityInput = document.getElementById('create-task-priority');
const settingsInput = document.getElementById('create-task-settings');
const submitBtn = document.getElementById('create-task-submit-btn');

const filterValues = {
    "Type": null,
    "Status": null,
    "Cron": null,
};

function reload() {
    const filteredValues = Object.fromEntries(
        Object.entries(filterValues)
            .filter(([_, val]) => val !== null && val !== undefined && val !== 'All')
            .map(([key, value]) => [key, String(value)])
    );

    requestQueue.enqueue({
        method: 'POST',
        url: '/ScheduledTask/ScheduledTasksPartial',
        data: {Values: filteredValues},
        options: {
            headers: {
                'RequestVerificationToken': GetAntiForgeryToken()
            },
            withCredentials: true
        },
        beforeSend: () => {
        },
        setProgress: (val) => {
        }
    })
        .then(async response => {
            const data = await response.data;
            document.getElementById('scheduled_tasks_partial').innerHTML = data
                .trim()
                .replaceAll('\n', '')
                .replaceAll('\r', '');

            convertUtcToLocal();
        })
        .catch(error => {
            console.error(`Ошибка при выполнении AJAX-запроса: ${error}`);
        });
}

function setDefaultDateTime() {
    const now = new Date();

    const offsetMs = now.getTimezoneOffset() * 60 * 1000;

    dateTimeInput.value = new Date(now.getTime() - offsetMs).toISOString().slice(0, 19);
}
