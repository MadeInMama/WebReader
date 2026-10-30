const connection = new signalR.HubConnectionBuilder()
    .withUrl("/ScheduledTaskHub", {
        skipNegotiation: true,                          // Пропускаем этап создания ID сессии
        transport: signalR.HttpTransportType.WebSockets // Работаем ТОЛЬКО через чистые WebSockets
    })
    .build();

connection.on("ScheduledTaskHub", reload)

connection.start()
    .then(() => {
    })
    .catch(err => console.error(err.toString()));
