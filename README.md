# SuezObservability

Exercice .NET 10 pour suivre le parcours d'un message grâce aux **traces, métriques et logs**.

## Architecture

```text
Producer → RabbitMQ → PersistenceApi → PostgreSQL
                           ↓ HTTP
                        CacheApi → Redis
```

- **Producer** : console interactive qui envoie un message.
- **PersistenceApi** : consomme le message, l'enregistre dans PostgreSQL puis appelle CacheApi.
- **CacheApi** : écrit le message dans Redis.
- **Shared** : modèle `MessageData` sous forme de record, configuration RabbitMQ et configuration commune d'OpenTelemetry. Chaque application possède sa propre classe `Telemetry`.

## Lancement

Prérequis : Docker avec Compose. Le SDK .NET 10 sert uniquement aux tests locaux.

```powershell
docker compose up -d --build
docker compose run --rm --build producer
```

Le Producer est lancé séparément car il attend une saisie console. Son profil `tools` l'exclut du démarrage normal de la stack. Entrer un message non vide.

Sur un volume neuf, PostgreSQL crée automatiquement la table `messages` avec `postgres/init.sql`.

| Service | Adresse |
| --- | --- |
| Grafana | http://localhost:3000 |
| RabbitMQ | http://localhost:15672 (`guest` / `guest`) |
| PersistenceApi | http://localhost:5125/health |
| CacheApi | http://localhost:5126/health |

Les routes `/health` indiquent que les APIs répondent, sans vérifier leurs dépendances.

## Choix techniques

- Le Producer utilise un **Host .NET** pour charger la configuration et gérer le démarrage et l'arrêt des services, dont OpenTelemetry.
- Les paramètres RabbitMQ sont dans les `appsettings.json`, chargés avec `IOptions<RabbitMqOptions>`. Docker remplace le host local via `RabbitMq__HostName`.
- La factory et la connexion RabbitMQ sont des **singletons par application**. Plusieurs envois dans le même processus réutilisent la connexion. `MessageFactory` est injectée.
- L'ACK arrive après les écritures SQL et cache. En cas d'erreur, le message est rejeté et supprimé de RabbitMQ : **une seule tentative, sans retry ni file d'échec**.
- La clé primaire PostgreSQL et la clé Redis basée sur l'identifiant évitent les doublons si le même message est reçu plusieurs fois.

## Observabilité

Les signaux passent par le Collector OTLP vers Grafana LGTM : **Tempo** pour les traces, **Prometheus** pour les métriques et **Loki** pour les logs.

Les spans RabbitMQ et Redis sont manuels. Les appels SQL et HTTP sont instrumentés automatiquement. Le contexte de trace est transmis dans les headers RabbitMQ, puis automatiquement via HTTP, pour conserver le même `TraceId` jusqu'à Redis.

### Métriques

| Métrique Prometheus | Ce qu'elle mesure |
| --- | --- |
| `messages_total` | Résultat du traitement, avec `status="processed"` ou `status="failed"` |
| `cache_operations_total` | Écritures Redis, avec `status="success"` ou `status="failed"` |
| `message_duration_seconds_*` | Histogramme de durée en secondes : `_bucket`, `_sum`, `_count` |

Les compteurs cumulent les événements depuis le démarrage du service. Comparer leur valeur avant et après un test :

- message normal : **+1 processed**, **+1 success cache** ;
- panne Redis : **+1 failed message**, **+1 failed cache** ;
- message vide reçu par PersistenceApi : **+1 failed message**, aucun appel au cache.

### Dans Grafana

Le dashboard **SuezObservability** est chargé automatiquement : compteurs acceptés/rejetés, durée moyenne et courbes messages/Redis.

Dans **Explore → Tempo**, rechercher le `TraceId` affiché dans les logs du Producer ou de PersistenceApi. Dans **Explore → Loki**, utiliser :

```logql
{service_name="SuezObservability.PersistenceApi"}
```

## Tester une panne Redis

```powershell
docker compose stop redis
docker compose run --rm producer
docker compose logs -f persistenceapi cacheapi
```

Envoyer un message, puis regarder la métrique d'échec, la trace jusqu'à Redis et le détail dans les logs. Le message est supprimé de RabbitMQ après l'échec, mais sa ligne PostgreSQL peut déjà exister.

```powershell
docker compose start redis
```

Attendre le retour de Redis, puis envoyer un nouveau message pour vérifier la reprise.

Le Producer refuse une saisie vide. Pour tester ce cas dans PersistenceApi, publier depuis l'interface RabbitMQ un message JSON avec un contenu `Message` vide.

## Tests

```powershell
dotnet test SuezObservability.slnx
docker compose config --quiet
```

Les six tests couvrent la création et la sérialisation, le contenu vide, l'ordre SQL → cache et l'arrêt du traitement après une erreur SQL.

<details>
<summary>Si des volumes d'une ancienne version existent déjà</summary>

Pour appliquer le script SQL sur un volume existant :

```powershell
Get-Content postgres/init.sql -Raw | docker compose exec -T postgres psql -U suez -d suezdb -v ON_ERROR_STOP=1
```

Si `suez-messages` possède encore des paramètres `x-dead-letter-*`, arrêter PersistenceApi et recréer uniquement cette queue lorsqu'elle est vide et sans message non acquitté. Sauvegarder les messages éventuels avant toute suppression.

Le hostname RabbitMQ est fixé à `rabbitmq` pour retrouver les données après un `down` puis `up`. Un ancien volume utilisant un hostname automatique a un autre nom de noeud : sauvegarder ses messages avant de recréer le broker, car le nouveau noeud ne les charge pas automatiquement.

</details>
