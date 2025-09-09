# Imagino.Api

API de geração de imagens desenvolvida em ASP.NET Core 8.

## Executando em produção com Docker

1. **Build** da imagem:
   ```bash
   docker build -t imagino-api .
   ```
2. **Run** do container expondo a porta de aplicação (5000):
   ```bash
   docker run -p 5000:5000 imagino-api
   ```

A aplicação utiliza as configurações de `appsettings.json` em produção.

## Variáveis de ambiente

| Nome | Descrição |
| --- | --- |
| `EMAIL__PROVIDER` | Provedor de e-mail (ex.: `Resend`) |
| `RESEND__API_KEY` | API key do Resend |
| `EMAIL__FROM` | Endereço de envio |
| `EMAIL__FROM_NAME` | Nome exibido no envio |
| `FRONTEND__BASEURL` | URL base do frontend para links de verificação e reset |
