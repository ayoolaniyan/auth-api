#!/usr/bin/env bash
# Deletes the local kind cluster created by k8s/deploy.sh (including the database volume).
# To keep the cluster and only remove the app: helm uninstall auth-services -n auth-services --kube-context kind-auth-services
set -euo pipefail

kind delete cluster --name auth-services
