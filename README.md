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

A aplicação usa defaults seguros do JSON e credenciais de variáveis de ambiente.
O comando acima exige configuração externa antes de iniciar. Veja
[CONFIGURATION.md](CONFIGURATION.md) e [SECURITY_OPERATIONAL_PLAN.md](SECURITY_OPERATIONAL_PLAN.md).

## Variáveis de ambiente

| Nome | Descrição |
| --- | --- |
| `EMAIL__PROVIDER` | Provedor de e-mail (ex.: `Resend`) |
| `Resend__ApiKey` | Chave canônica do Resend; aliases legados somente para transição |
| `EMAIL__FROM` | Endereço de envio |
| `Email__FromName` | Nome exibido no envio |
| `FRONTEND__BASEURL` | URL base do frontend para links de verificação e reset |
