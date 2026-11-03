# Synchronisation AXIS V0.8 — plan d'exécution

Objectif : publier les sources locales reconstructibles et une documentation fidèle sur shinobione/shino-axis, sans perte des travaux existants.

Exécution directe demandée par l'utilisateur ; pas de refonte applicative. Base initiale : 5a32a5ce1d50f52e5b438d5fa9b9f162ea14a9de, branche codex/p1-foundation-lab. GitHub main vérifié à la même base avant modifications.

- [x] Lire les instructions, README/architecture/roadmap, livraisons V0.7/V0.8 et sources ciblées ; inventorier les changements préexistants.
- [x] Réécrire README/architecture/roadmap ; ajouter CHANGELOG, guide de développement et qualification Chrome bornée ; distinguer les rapports historiques.
- [x] Compléter les exclusions et sélectionner explicitement sources/tests/docs/laboratoire historique sans données privées ni binaires.
- [x] Reconstruire à partir de cette sélection isolée, exécuter la suite normale, contrôler liens/diff/exclusions.
- [x] Consultation Advisor en lecture seule sur la publication publique précise ; résoudre les constats avant publication autorisée.
Publication autorisée : recontrôler le distant, créer le commit explicite et pousser sans force ; vérifier le SHA distant ou ouvrir une PR si nécessaire. Résultat effectif communiqué dans le bilan final de mission.

Contraintes : aucun autre dépôt/appareil/service modifié, aucun historique ChatGPT rattrapé, aucune release binaire. Les sources synthétiques et validations historiques ne prouvent pas une recette réelle V0.8.

Fichiers principaux : README.md, CHANGELOG.md, .gitignore, docs/ARCHITECTURE.md, docs/ROADMAP.md, docs/DEVELOPMENT.md, docs/CHROME-COLLECTION-VALIDATION.md. Sources/tests/rapports préexistants conservés ; ADR/P1 signalés comme historiques.


Revue Advisor : avis favorable à la publication publique des 98 candidats, sans bloqueur identifié. Accord humain déjà fourni dans la demande de mission, section 8. Nettoyage final des seuls espaces de fin de ligne/fichier préexistants ; aucune différence fonctionnelle des sources/tests par rapport à la copie validée.
