{{/* Common labels. Call with (dict "root" $ "name" "<component>"). */}}
{{- define "auth-services.labels" -}}
{{ include "auth-services.selectorLabels" . }}
app.kubernetes.io/part-of: auth-services
app.kubernetes.io/managed-by: {{ .root.Release.Service }}
helm.sh/chart: {{ .root.Chart.Name }}-{{ .root.Chart.Version }}
{{- end -}}

{{/* Selector labels. Call with (dict "root" $ "name" "<component>"). */}}
{{- define "auth-services.selectorLabels" -}}
app.kubernetes.io/name: {{ .name }}
app.kubernetes.io/instance: {{ .root.Release.Name }}
{{- end -}}
