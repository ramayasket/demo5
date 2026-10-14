# demo5

API управления задачами на .NET 10, ASP.NET Core Controllers, PostgreSQL и RabbitMQ.

## Запуск

Параметры подключения к PostgreSQL и RabbitMQ уже заданы в `TaskApi/appsettings.json`.
При старте приложение автоматически применяет миграции.

```powershell
dotnet run --project TaskApi
```

Swagger UI после запуска доступен по адресу `/swagger`.

## Проверка

```powershell
dotnet test
```

Интеграционный тест запускает API в памяти, создаёт и завершает задачу, затем
проверяет событие через тестовую реализацию `ITaskEventPublisher`.

## API

- `POST /tasks` — создать задачу;
- `GET /tasks` — получить все задачи;
- `PUT /tasks/{id}/complete` — завершить задачу;
- `DELETE /tasks/{id}` — удалить задачу.

Сообщение о завершении ставится во внутреннюю очередь только после успешного
сохранения в PostgreSQL. Фоновый издатель отправляет его в обменник
`task.events` с routing key `task.completed`. Ошибка RabbitMQ не отменяет
завершение задачи и записывается в лог. При остановке очередь получает до пяти
секунд на завершение начатых публикаций.
