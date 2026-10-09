# SuezObservability

Exercice .NET 10 dont l'objectif est de rendre observable le parcours complet d'un message grâce aux **traces, métriques et logs**.

## Architecture

Le flux principal est :

```text
Producer
   ↓
RabbitMQ
   ↓
PersistenceApi
   ├── PostgreSQL
   ↓ HTTP
CacheApi
   ↓
Redis
```

- **Producer** : application console qui envoie un message dans RabbitMQ.
- **PersistenceApi** : consomme le message, l'enregistre dans PostgreSQL puis appelle CacheApi.
- **CacheApi** : enregistre le message dans Redis.
- **Shared** : contient les éléments communs liés à l'observabilité et à la configuration des queues RabbitMQ.

Les signaux OpenTelemetry sont envoyés en **OTLP** vers un Collector puis vers la stack Grafana LGTM :

- **Tempo** : traces
- **Prometheus** : métriques
- **Loki** : logs
- **Grafana** : visualisation

---

## Lancement

### Prérequis

- Docker
- Docker Compose
- .NET 10 uniquement pour lancer les tests localement

### Démarrer la stack

```powershell
docker compose up -d --build
```

### Envoyer un message

Le Producer est interactif et se lance séparément :

```powershell
docker compose run --rm --build producer
```

Entrer ensuite un message non vide.

---

## PostgreSQL

Au premier démarrage, PostgreSQL exécute automatiquement :

```text
postgres/init.sql
```

Le script crée la table `messages`.

L'identifiant du message est une clé primaire, ce qui évite de créer un doublon lorsqu'un même message est retraité.

Si le volume PostgreSQL existait avant l'ajout du script :

```powershell
Get-Content postgres/init.sql -Raw |
docker compose exec -T postgres psql -U suez -d suezdb -v ON_ERROR_STOP=1
```

---

## Accès aux services

| Service | Adresse |
| --- | --- |
| Grafana | http://localhost:3000 |
| RabbitMQ | http://localhost:15672 |
| PersistenceApi | http://localhost:5125/health |
| CacheApi | http://localhost:5126/health |

Identifiants RabbitMQ locaux :

```text
guest / guest
```

Les routes `/health` indiquent uniquement que les APIs répondent. Elles ne garantissent pas que PostgreSQL, Redis ou RabbitMQ soient disponibles.

---

## Observabilité

### Traces

Des spans permettent de suivre le message pendant les principales étapes :

```text
message.publish
└── message.consume
    ├── postgres.insert
    └── appel HTTP CacheApi
        └── redis.set
```

Le contexte de trace est propagé :

- manuellement dans les headers RabbitMQ ;
- automatiquement lors de l'appel HTTP grâce à l'instrumentation OpenTelemetry de `HttpClient` et ASP.NET Core.

Le même **TraceId** permet donc de suivre un message du Producer jusqu'à Redis.

### Métriques

Les métriques principales sont :

```text
messages_total
cache_operations_total
message_duration_seconds
```

`messages_total` compte le résultat final de chaque livraison, après les éventuelles tentatives, selon son statut :

```text
status="processed"
status="failed"
```

`cache_operations_total` compte chaque tentative d'écriture Redis, avec `status="success"` ou `status="failed"`.

Ces compteurs cumulent les événements depuis le démarrage du service. Un message en échec après trois tentatives Redis compte donc pour un échec côté messages et trois échecs côté cache.

`message_duration_seconds` est un histogramme de la durée totale du traitement, en secondes, tentatives et pauses comprises. Prometheus l'expose avec les suffixes `_bucket`, `_sum` et `_count`.

### Logs

Les logs sont structurés et exportés avec OpenTelemetry.

Ils contiennent notamment des informations comme le `MessageId` et le `TraceId`, ce qui permet de retrouver facilement la trace correspondant à une erreur.

---

## Dashboard Grafana

Le dashboard **SuezObservability** est chargé automatiquement dans Grafana.

Il permet notamment de suivre :

- les messages traités ;
- les messages en erreur ;
- les opérations Redis ;
- la durée de traitement.

Pour consulter une trace :

1. ouvrir Grafana ;
2. aller dans **Explore** ;
3. sélectionner **Tempo** ;
4. rechercher `SuezObservability.Producer` ou utiliser un `TraceId` présent dans les logs.

Pour consulter les logs dans Loki :

```logql
{service_name="SuezObservability.PersistenceApi"}
```

---

## Gestion des erreurs RabbitMQ

Un message n'est acquitté qu'après la fin du traitement.

En cas d'erreur technique, le consumer effectue au maximum **3 tentatives**, avec **5 secondes d'attente** entre chaque tentative.

Le prefetch RabbitMQ est limité à `1` pour éviter d'accumuler plusieurs messages en traitement lors d'une panne.

Après le dernier échec, le message est envoyé dans :

```text
suez-messages.failed
```

Les messages invalides sont également envoyés directement dans cette file.

Ils ne sont pas rejoués automatiquement afin de pouvoir analyser l'erreur avant une éventuelle remise en file.

---

## Exemple de diagnostic : Redis indisponible

Arrêter Redis :

```powershell
docker compose stop redis
```

Puis envoyer un message :

```powershell
docker compose run --rm producer
```

Observer ensuite :

```powershell
docker compose logs -f persistenceapi cacheapi
```

Dans Grafana :

- les **métriques** indiquent qu'une erreur apparaît ;
- la **trace** permet de voir que l'échec se produit au niveau de Redis ;
- les **logs** donnent le détail de l'erreur.

Après trois échecs, le message est placé dans `suez-messages.failed`.

Redémarrer Redis :

```powershell
docker compose start redis
```

Puis envoyer un nouveau message pour vérifier le retour à la normale.

---

## Idempotence

PostgreSQL peut avoir enregistré un message avant qu'une erreur Redis ne se produise.

Lors d'une nouvelle tentative avec le même identifiant :

- PostgreSQL ne crée pas de doublon ;
- Redis utilise la même clé.

Cela permet de reprendre le traitement sans dupliquer les données.

---

## Mise à jour d'une ancienne stack

La nouvelle queue utilise une file d'échec. RabbitMQ refuse de modifier les paramètres d'une queue existante : arrêter PersistenceApi, puis supprimer uniquement la queue `suez-messages` si elle est vide, avant de la recréer.

Le hostname RabbitMQ est maintenant fixé à `rabbitmq` pour conserver le même nom de noeud après un `down` puis `up`. Un ancien volume créé avec un hostname automatique utilise un autre nom de noeud : avant de recréer ce broker, traiter ou sauvegarder les messages de toutes ses queues. Les anciennes données restent dans le volume mais ne sont pas chargées automatiquement par le nouveau noeud.

## Tests

Lancer les tests :

```powershell
dotnet test SuezObservability.slnx
```

Vérifier également la configuration Docker :

```powershell
docker compose config --quiet
```

Les tests couvrent notamment :

- la création d'un message ;
- le rejet d'un message vide ;
- l'ordre PostgreSQL → CacheApi ;
- l'arrêt du traitement lorsqu'une erreur PostgreSQL se produit.

Les vérifications Docker ont aussi été réalisées sur des volumes neufs :

- plusieurs messages valides, caractères spéciaux et doublon avec le même identifiant ;
- dix entrées invalides via RabbitMQ et HTTP ;
- pannes Redis, PostgreSQL et CacheApi, puis reprise sans doublon SQL ;
- arrêt du consumer pendant un traitement et reconnexion après redémarrage de RabbitMQ ;
- recréation complète des conteneurs avec conservation des données, puis nouvel envoi ;
- traces complètes dans Tempo, métriques du dashboard et logs dans Loki.

Les six tests .NET passent également dans un conteneur Linux avec le SDK .NET 10.
