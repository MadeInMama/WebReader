document.getElementById('create-task').onsubmit = (e) => {
    e.preventDefault();

    typeSelect.disabled = true;
    dateTimeInput.disabled = true;
    priorityInput.disabled = true;
    settingsInput.disabled = true;
    submitBtn.disabled = true;
    submitBtn.innerText = 'Sending...';

    const dateTimeOffset = new Date(dateTimeInput.value).toISOString();

    requestQueue.enqueue({
        method: 'POST',
        url: '/ScheduledTask/CreateTask',
        data: {
            Type: parseInt(typeSelect.value),
            HaveToStartAt: dateTimeOffset,
            Priority: parseInt(priorityInput.value),
            Settings: settingsInput.value
        },
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
        })
        .catch(error => {
            console.error('Ошибка при отправке:', error);
        })
        .finally(_ => {
            typeSelect.disabled = false;
            dateTimeInput.disabled = false;
            priorityInput.disabled = false;
            settingsInput.disabled = false;
            submitBtn.disabled = false;
            setDefaultDateTime();
            submitBtn.innerText = 'Confirm';
        });
};

typeSelect.onchange = (e) => {
    updateCreateTaskFromField(e.target.value)
}

function updateCreateTaskFromField(value) {
    const defs = taskConfigDefinitions[value];

    priorityInput.value = defs.Priority;
    settingsInput.value = JSON.stringify(defs.Settings, null, 2);
}
