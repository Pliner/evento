revision:
	dotnet ef migrations add "${message}" --project Evento -o "./Db/Migrations"

apply:
    docker-compose run --rm --entrypoint="./efbundle" evento
