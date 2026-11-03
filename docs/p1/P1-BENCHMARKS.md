# P1 — Benchmarks

**Statut : NON MESURÉ pour les deux candidats. Aucun benchmark long exécuté.**

Les tests fonctionnels du laboratoire passent, mais B reste un hôte SDK synthétique sans l'application complète/GUI. A n'a pas de qualification visuelle du shell. Comparer leurs seuls processus de test ne satisferait pas le périmètre demandé. Les essais TLS/pin/Digest et l'isolation des processus de modules ne sont pas qualifiés. Les barrières du comparatif complet restent ouvertes.

| Mesure | Budget P1 | A | B complet |
|---|---:|---|---|
| RAM privée repos | ≤300 Mio | NON MESURÉ | NON MESURÉ |
| RAM privée charge | ≤450 Mio | NON MESURÉ | NON MESURÉ |
| CPU médian / p95 repos | ≤0,5 % / ≤1,5 % | NON MESURÉ | NON MESURÉ |
| CPU médian / p95 charge | ≤2 % / ≤5 % | NON MESURÉ | NON MESURÉ |
| Latence événement→client p95 | ≤250 ms | NON MESURÉ | NON MESURÉ |

Aucun CSV de mesures n'est fabriqué. Le fichier `lab/p1/shared/benchmarks.csv` contient seulement l'en-tête prévu, zéro observation. Les durées courtes dans `results-*.json` sont des temps de suite fonctionnelle, avec démarrages multiples : **ce ne sont pas des benchmarks de consommation ou de latence**.

## Protocole à exécuter après levée des barrières

Trois répétitions par candidat, ordre A/B puis B/A puis A/B. Chaque essai : 60 s chauffe + 600 s repos + 600 s charge, soit 21 min ; six essais, 126 min hors préparation. Un seul candidat actif. Corpus, horloge simulée, fréquence d'injection et consommation strictement identiques. Définir avant mesure la charge (par exemple une publication par seconde et deux lecteurs à cette même cadence), le nombre de clients et le nombre d'événements ; ne pas les adapter au candidat après observation.

Relever OS/runtime exacts, CPU logique disponible et ses éventuelles contraintes, alimentation, mémoire installée, commits et hashes corpus/schémas/configuration. Identifier et conserver PID + instant de création de chaque processus possédé, shell GUI et enfants de plugins inclus. L'instrumentation doit détecter créations/fins de processus ou marquer une collecte incomplète ; une liste ponctuelle de PID ne prouve pas l'arborescence complète. Aucun arrêt de processus tiers.

CPU normalisé sur capacité totale : `100 × somme(delta CPU secondes des processus possédés) / (delta temps monotone secondes × nombre de processeurs logiques disponibles)`. La même formule s'applique à A et B ; documenter affinité/cgroups/VM si la capacité est contrainte. Pas de simple division d'un compteur instantané non documenté. Mémoire privée : somme des Private Bytes de l'arborescence, divisée par 1 048 576 pour Mio ; working set séparé si relevé. Ne pas additionner du working set partagé pour prétendre mesurer la mémoire privée.

Échantillonnage ressource proposé : 1 Hz, horloge monotone ; export brut puis médiane, p95 (méthode nearest-rank : rang `ceil(0,95×n)`) et maximum par phase et répétition. Échantillon manquant/lecture refusée : invalide et expliqué, jamais remplacé par zéro. Garder les six résultats séparés avant toute agrégation.

Latence : ID de corrélation, instant monotone d'émission synthétique et instant de réception observée côté client, même horloge de mesure ; ne pas soustraire deux horloges murales. Compter les pertes/timeouts en plus des latences reçues ; le p95 des seuls succès ne masque pas des événements perdus. Le client GUI doit être actif et les domaines réellement consommés. Les dates contractuelles fixes restent indépendantes de cette instrumentation monotone.

Ce document spécifie le protocole ; **le collecteur complet CPU/RAM/arborescence/latence n'est pas implémenté**. Il reste à livrer et vérifier avec le démarrage complet confiné. Aucun résultat ni avantage de performance ne peut être déduit des tailles de code, du nombre de packages ou des temps de tests actuels.
