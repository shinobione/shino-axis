# Collecte Chrome structurée — qualification rapportée

Ce compte rendu complète le rapport V0.7, qui précédait cette recette. Source : rapport local `artifacts/collector-structured/VALIDATION.md`, consulté pendant la synchronisation documentaire. L'artefact et le profil privé ne sont pas publiés. Aucune nouvelle recette navigateur n'a été effectuée pour cette synchronisation.

## Résultat réel du 03/11/2026

L'extension Chrome existante a acquis une branche active de **527 messages** via la source structurée ChatGPT, avec attribution au projet attendu et une seule preuve. Après rechargement inchangé et 35 secondes d'observation, source/preuve, empreintes calculées en mémoire et horodatage de persistance étaient inchangés. Aucun contenu de conversation, marqueur de test, identifiant privé ou empreinte de données privées n'est reproduit ici.

Le rapport identifie l'exécutable AXIS **V0.7** et sa santé HTTP 200 sur 4187 lors de cette recette. Cela prouve un parcours extension → récepteur PROJECTS existant ; ce n'est pas une requalification du package CODEX V0.8 ni du compte Codex réel.

## Périmètre technique rapporté

Trois fichiers de l'extension externe ont été corrigés : `collector.js`, `catchup-api.js`, `background.js`. Identité Chrome, manifeste, préférences et endpoint conservés. Le DOM ne fournit plus le contenu ; l'API structurée vérifie identité et chaîne active, compte user/assistant avant réduction de taille et utilise une empreinte commune. Timeouts, annulation, réponses tardives et retries bornés ; pas de substitution par un snapshot DOM.

Tests ciblés rapportés : branche complète/alternatives, compteur, identité, cycle/parent manquant, finalité du dernier assistant, déduplication, navigation obsolète, timeouts/abort et réponses 429 sans corps privé. Ils appartiennent à la recette de l'extension externe ; leur code n'est pas inclus dans les sources AXIS publiées.

## Limites

API ChatGPT interne non documentée, susceptible de changer. Pas de certification exhaustive des formats multimodaux, autres onglets ou redémarrage complet de Chrome. Certains scénarios concurrents/pannes sont synthétiques. Pas de rattrapage historique complet lancé ni demandé. Pas de parité CONTROL exhaustive ou de retrait de CONTROL déduit de cet essai.
