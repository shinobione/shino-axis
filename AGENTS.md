# SHINO // AXIS — Instructions pour agents de développement

## Mission

AXIS est une future plateforme Windows modulaire qui fédère des services SHINO existants et communique avec Android TV, SmallTV et des appareils futurs. La priorité est la **réutilisation** et la **compatibilité**. Le langage est une décision à prendre sur preuves, pas un prérequis.

## À lire en premier

`README.md`, `docs/ARCHITECTURE.md`, `docs/ROADMAP.md`. Avant toute proposition de migration, consulter les documents techniques et le code réellement à jour de chaque projet concerné, en distinguant GitHub, branches locales, artefacts installés et état physique.

## Garde-fous

1. Travail d'audit **lecture seule** par défaut. Pas de modification des dépôts existants, service Windows, VM, réseau, appareil, firmware, secrets ni publication sans autorisation précise.
2. Ne pas faire démarrer LINK en double à côté du collecteur actuellement repris par RELAY ; ne pas créer deux producteurs pour la même donnée.
3. Préserver les contrats Android HUB v2 existants, les TTL et les états inconnus/périmés ; jamais d'état LIVE simulé.
4. Conserver CONTROL loopback-only et son extension Chrome isolée ; ne pas exposer son serveur au LAN comme solution rapide.
5. Ne pas copier de code tiers sans vérification explicite de licence, dépendances et notices.
6. Les changements sur SmallTV, Freebox, Android réel, OTA et HUB nécessitent une autorisation distincte, même si les tests simulés passent.
7. Aucun choix technologique irréversible sans comparaison documentée et scénario de retour arrière.
8. Éviter la suringénierie : contrats minimaux démontrés sur deux sources et deux clients simulés avant tout plugin marketplace ou moteur d'automatisation complexe.
9. Préserver les données privées ; ne jamais committer secrets, tokens, journaux de conversations, chemins confidentiels, reçus ou dumps matériels.
10. Minimiser le quota Codex : un audit cadré, une sortie concise, pas d'itérations cosmétiques. Ne solliciter un Advisor qu'en cas de blocage sérieux répété ou de décision critique selon les règles globales.

## Format de rapport de mission

- Périmètre lu et SHA / état des sources (dépôt vs local vs appareil).
- Faits prouvés, hypothèses, contradictions et données manquantes.
- Décisions, alternatives, risques et coûts de migration.
- Tests exécutés avec résultats précis.
- Une prochaine action concrète, avec critère de réussite.
- Déclaration explicite des opérations non effectuées.

**Ne pas coder AXIS par défaut pendant un audit de découverte.**
