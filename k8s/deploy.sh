#!/usr/bin/env bash
# Builds the service images, loads them into a local kind cluster and installs the
# auth-services Helm chart. Safe to re-run: it reuses the cluster and upgrades the release.
#
# Usage (from anywhere): k8s/deploy.sh
set -euo pipefail

CLUSTER=auth-services
CONTEXT="kind-${CLUSTER}"
NAMESPACE=auth-services
RELEASE=auth-services
TAG=local

K8S_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(dirname "$K8S_DIR")"

for tool in docker kind kubectl helm; do
  command -v "$tool" >/dev/null || { echo "error: $tool is not installed" >&2; exit 1; }
done

if [[ ! -f "$REPO_ROOT/.env" ]]; then
  echo "error: $REPO_ROOT/.env not found. Run: cp .env-example .env and set MSSQL_SA_PASSWORD" >&2
  exit 1
fi

# Every kubectl/helm call names the kind context explicitly, so the current
# kubectl context (which may be a real cluster) is never touched.
if ! kind get clusters | grep -qx "$CLUSTER"; then
  kind create cluster --config "$K8S_DIR/kind-config.yaml"
else
  # kind cannot add port mappings to an existing cluster.
  if ! docker port "${CLUSTER}-control-plane" 30500 >/dev/null 2>&1; then
    echo "warning: this cluster predates the Consul port mapping, so the Consul UI will not be on" >&2
    echo "         http://localhost:8500. Run k8s/teardown.sh and re-run this script to recreate it." >&2
  fi
  if ! docker port "${CLUSTER}-control-plane" 30300 >/dev/null 2>&1; then
    echo "warning: this cluster predates the Grafana port mapping, so Grafana will not be on" >&2
    echo "         http://localhost:3000. Run k8s/teardown.sh and re-run this script to recreate it." >&2
  fi
fi

# Image name -> project folder (each has its own Dockerfile; build context is the repo root).
declare -a SERVICES=(identityserver:IdentityServer inventories-api:Inventories.API apigateway:ApiGateway inventories-client:Inventories.Client)
for entry in "${SERVICES[@]}"; do
  name="${entry%%:*}"
  project="${entry#*:}"
  image="auth-services/${name}:${TAG}"
  docker build -t "$image" -f "$REPO_ROOT/$project/Dockerfile" "$REPO_ROOT"
  kind load docker-image "$image" --name "$CLUSTER"
done

kubectl --context "$CONTEXT" create namespace "$NAMESPACE" --dry-run=client -o yaml \
  | kubectl --context "$CONTEXT" apply -f -

# The SQL Server password comes from .env and is kept out of Helm values/release history.
kubectl --context "$CONTEXT" -n "$NAMESPACE" create secret generic identity-db-secret \
  --from-env-file="$REPO_ROOT/.env" --dry-run=client -o yaml \
  | kubectl --context "$CONTEXT" apply -f -

helm upgrade --install "$RELEASE" "$K8S_DIR/charts/auth-services" \
  --kube-context "$CONTEXT" \
  --namespace "$NAMESPACE" \
  --set image.tag="$TAG" \
  --wait --timeout 10m

# Pods keep the old image when the tag is unchanged; restart so rebuilt images are used.
kubectl --context "$CONTEXT" -n "$NAMESPACE" rollout restart deployment
kubectl --context "$CONTEXT" -n "$NAMESPACE" rollout status deployment --timeout 5m

kubectl --context "$CONTEXT" -n "$NAMESPACE" get pods
