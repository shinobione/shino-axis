# P0 — Contrats à conserver

Références et niveaux : [inventaire](P0-INVENTORY.md). Les contrats relevés sont des sources locales/GitHub, pas une négociation avec les appareils installés.

## HUB / Android v2

| Élément | Invariant relevé | Preuve |
|---|---|---|
| Version | Routes `/hub/v1/...` et document `schemaVersion=2` : deux versions distinctes. Ne pas renommer les routes pour « harmoniser » v2. | L : R/hub/protocol, V/HubClient. |
| Enveloppe | generatedAt, sampleKind TEST/DEMO/LIVE, domaines indépendants telemetry/media/codex. capturedAt conserve l'instant source ; une nouvelle enveloppe ne rajeunit pas la mesure. | L/D : R/schema, B/CONTRACT, V/HubModels. |
| Fraîcheur | TTL telemetry **6 s**, media **10 s**, codex **900 s**. CURRENT si âge strictement inférieur au TTL ; expiration/reset quota rendent périmé. Availability AVAILABLE/PARTIAL/UNAVAILABLE et freshness CURRENT/STALE/UNKNOWN indépendantes. | L : R/hub/core, V/DomainMeta ; D : B/CONTRACT. |
| Absence | Capteur indisponible → value null et available false ; zéro reste une vraie mesure possible. NO_SESSION ≠ échec source ; aucune activité déduite des quotas. | D : B/CONTRACT ; L : R/sources, V/HubModels. |
| Unités | percent, GiB, celsius, milliseconds, percent_remaining. Les formes CLI usedPercent sont inversées une seule fois ; les restants historiques ne le sont pas. | L : R/codex_live.quota_document, history_document ; D : B/CONTRACT. |
| Validation | Schéma fermé + règles temporelles/sémantiques. Refus des clés dupliquées, non-finis, formes excessives et dates incohérentes ; JSON ≤262144 octets, profondeur 32 décrite. JSON Schema seul ne suffit pas. | L : R/schema, R/sources ; D : B/CONTRACT, R/SOURCE-CONTRACTS. |
| Autorisation | LINK possède telemetry/media ; PERISCOPE possède codex ; TV_HOME lit. source.component doit correspondre à l'identité autorisée. LAB existe dans le moteur de test, pas comme droit implicite de production. | L/D : R/daily, hub/core, SOURCE-CONTRACTS. |
| TLS | CA explicite, vérification nom/IP SAN et pin SHA-256 du certificat feuille DER ; TLS 1.3 ; pas de redirection. Pin DER ≠ pin SPKI. Bearer ne doit pas partir avant la qualification TLS. | L : V/HubClient ; D : R/SOURCE-CONTRACTS. |
| Anti-rejeu | Horodatage + digest, sans champ sequence inventé ; égal + contenu identique = idempotent ; égal divergent ou plus ancien = conflit. Watermarks durables et verrou d'écrivain. | L : R/hub/core, reliable ; D : SOURCE-CONTRACTS. |

Android `DomainMeta.label` autorise une présentation « Actualisation en attente » pendant une grâce d'affichage, mais `isCurrent` conserve l'échéance réelle [L]. AXIS ne doit pas transformer cette présentation en TTL allongé.

### Surface HTTP relevée

| Route | Usage et réponses |
|---|---|
| PUT `/hub/v1/snapshot` | Publication v2, 201 changement / 200 idempotence. |
| GET `/hub/v1/snapshot` et `/{telemetry,media,codex}` | Agrégat ou domaine sous ACL ; 503 si état non qualifié/absent selon moteur. |
| GET `/hub/v1/status` | Présentation fraîcheur/durabilité. |
| PUT, HEAD, GET `/hub/v1/artwork/<sha256>` | Dépôt, présence, lecture selon rôle ; PUT 201/200. Référence de contenu, pas URL arbitraire. |
| DELETE `/hub/v1/artwork/unreferenced` | Surface historique réservée au publicateur média pour ses objets non référencés ; aucun usage AXIS proposé. |

Preuve L : R/hub/protocol ; détails D : R/SOURCE-CONTRACTS. JPEG ≤40960 octets, bornes HUB 1..512 pixels ; adaptateur RELAY documenté 192×192. Quatre objets /163840 octets et état agrégé 65536 octets sont des limites locales décrites, pas des capacités de tout HUB installé [D]. Les erreurs ACL, contrat, conflit, capacité ou ambiguïté ne justifient jamais un reset.

**Reprise [L/D : R/daily, reliable, SOURCE-CONTRACTS] :** les registres HOLD/UNKNOWN et intentions durables sont une frontière de sécurité. Conserver notamment les historiques UNKNOWN_NO_RETRY ; une incertitude après envoi ne devient ni ACK ni autorisation de retry. Distinguer le transport historique et le moteur quotidien avant de reprendre leur politique de repli.

## Divergence réseau en cours

V/HubModels accepte `telemetry.network`, V/schema contient `$defs/network` et sa référence ; ces fichiers sont modifiés/non suivis localement. R/schema inspecté n'a pas ce bloc et reste fermé [L ; empreintes inventaire]. V/docs/NETWORK-PC-INTEGRATION-2.1 décrit pourtant l'extension comme proposée/non appliquée et demande une migration coordonnée [D].

Conclusion : **extension préparée côté client, parité bout en bout non démontrée**. Ne pas émettre de champ network vers l'ancien validateur, ne pas prétendre mesurer la latence Internet, ne pas confondre vitesse de liaison et trafic. P1 doit rejouer ancien document, extension reconnue, extension inconnue et champ mal typé [H].

## Ancienne passerelle Android v1

V/companion/windows/gateway.py produit `schemaVersion=1`, utilise un serveur TLS/Bearer sur 8766 par défaut et peut projeter l'historique PERISCOPE ; options de collecte séparées [L]. Le schéma v1 strict ne peut pas recevoir arbitrairement des champs v2 [D : B/CONTRACT]. Garder un adaptateur explicite avec liste des pertes ; ne pas lancer cette passerelle en concurrence avec une autre acquisition [H].

## SmallTV : autre protocole et autres budgets

K/push_fsless_metrics vise POST `/api/v1/bridge/metrics` avec HTTP Digest et credentials propres au build. S/FirstBootBridge.cpp borne le corps à **16..384 caractères** avant parsing ; `FslessMetrics` stocke seulement en RAM, refuse les valeurs invalides et conserve le dernier bon échantillon [L].

S/FslessMetrics applique CPU/GPU 0..100, RAM utilisée 0..256, température -40..130, plus VRAM/puissance et indicateur GPU ; total RAM optionnel. Le périmé est calculé sur le temps depuis réception, **strictement >6000 ms**, différent de la limite HUB `âge >=6 s`. Le champ local `schema_version=2` ne désigne pas le schéma snapshot HUB [L].

Ne pas injecter le JSON HUB complet, des pochettes volumineuses ou des commandes dans ce POST numérique. Le chemin musique/scene des versions SmallTV ultérieures n'est pas établi par ce profil ; ni capacité installée ni firmware actif confirmés [N]. Les routes OTA/factory-return visibles ne sont pas des capacités ordinaires AXIS [H ; A/AGENTS].

## CONTROL : frontière locale

C/server-entry épinglé écoute `127.0.0.1:4177`. `/api/state` lit l'état, `/api/ingest/chatgpt-delta` et `/api/sync/github` sont des mutations ; aucune requête n'a été exécutée [G]. C/http-security contrôle loopback, Host, Origin et métadonnées navigateur ; collector autorisé par ID exact. La file de mutation est intra-processus, pas une garantie contre deux writers OS [G/D].

Le Bearer optionnel CONTROL n'est pas intégré à tous les clients existants selon README [D]. Ne pas l'activer dans P0, ni reprendre le port en LAN. L'adaptateur futur doit consommer une projection explicitement autorisée et expurgée ; `/api/state` complet peut contenir des données privées. L'isolation Chrome et l'absence de pont page-visible restent requises [H ; C/SECURITY].
