const connection = new signalR.HubConnectionBuilder()
    .withUrl("/ScheduledTaskHub")
    .build();

connection.on("ScheduledTaskHub", reload)

connection.start()
    .then(() => {
    })
    .catch(err => console.error(err.toString()));
