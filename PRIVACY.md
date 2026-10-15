# MoneyRecord privacy policy

*Effective 15 October 2026. [Español abajo](#política-de-privacidad-de-moneyrecord).*

MoneyRecord is a personal finance app that keeps your data on your device. It has no accounts, ads, analytics or
tracking.

## Data stored on your device
Your accounts, transactions, transfers, budgets, categories, receipt photos and settings are stored only in the app's
private storage on your device. The developer has no copy of them and no way to see them.

They leave the device only when you choose to:
- **Export or share a backup** (Settings → Backup & Restore), a CSV file or the logs. You pick where the file
  goes.
- **Android backup:** if your device's own backup is turned on, Android copies the database, receipt photos and
  settings to your Google account or to a new device, under Google's terms. Logs and caches are left out, and so is
  the database while **Encrypt database** is on: its key never leaves your phone.

## Crash reports (optional, off by default)
After a week of use the app asks once whether it may send crash reports; you can change the answer at any time in
**Settings → Send crash reports**. Only when you agree, and only in official builds, the app sends a report to the
developer through [Sentry](https://sentry.io) when an error happens. A report contains:
- the error and where in the app it happened (technical details such as the error message and stack trace);
- the app version and device information such as the Android version, device model, language and free memory;
- recent technical events in the app before the error (such as which screen was open).
- the approximate location (country, region and city) that Sentry works out from the connection's IP address.

A report doesn't include your amounts, descriptions or other records, screenshots, your name, email, user ID or device
ID, and the Sentry project doesn't store IP addresses. Reports are sent over an encrypted connection (HTTPS), used only
to find and fix bugs, never sold or shared, and deleted by Sentry when its retention period ends.

## Exchange rates (optional, off by default)
When you turn on **Settings → Exchange rates**, the app downloads daily exchange rates from
[Frankfurter](https://frankfurter.dev) (once a day, and when you tap **Update now**). A request contains only
currency codes and dates (for example USD, COP and a date range), never your amounts, descriptions or accounts. Like
any web request, it reveals your IP address to that server. The rates are stored on the device.

## Logs
The app writes technical logs (errors, app version, Android version) to its private storage, keeps them for 7 days at
most, and doesn't log your amounts or descriptions. They leave the device only when you use **Settings → Share logs**.

## Permissions
- **Camera:** only when you take a photo of a receipt.
- **Storage (Android 12 and older):** needed by the camera capture on those versions.
- **Internet:** used only for crash reports and exchange rates, when you have turned them on.
- **Notifications (Android 13 and newer):** asked when you turn reminders on, to remind you of bills and budgets.
  Reminders are created on the device; nothing is sent anywhere.
- **Biometrics / screen lock:** used only when you turn the app lock on. Android checks your fingerprint, face or
  screen lock; the app only learns whether it passed and stores nothing.

## Children
The app isn't directed at children and doesn't knowingly collect data from anyone.

## Changes and contact
Changes to this policy are published in this file, with a new effective date. Questions: open an issue at
https://github.com/SebastianOrtizA/MoneyRecord/issues.

---

# Política de privacidad de MoneyRecord

*Vigente desde el 15 de octubre de 2026.*

MoneyRecord es una app de finanzas personales que guarda tus datos en tu dispositivo. No tiene cuentas, publicidad,
analítica ni rastreo.

## Datos guardados en tu dispositivo
Tus cuentas, transacciones, transferencias, presupuestos, categorías, fotos de recibos y ajustes se guardan solo en el
almacenamiento privado de la app en tu dispositivo. El desarrollador no tiene copia de ellos ni forma de verlos.

Solo salen del dispositivo cuando tú lo decides:
- **Exportar o compartir una copia de seguridad** (Configuración → Copia de Seguridad y Restauración), un archivo
  CSV o los registros. Tú eliges a dónde va el archivo.
- **Copia de seguridad de Android:** si la copia de seguridad del dispositivo está activada, Android copia la base de
  datos, las fotos de recibos y los ajustes a tu cuenta de Google o a un dispositivo nuevo, según las condiciones de
  Google. Los registros y la caché quedan fuera.

## Informes de errores (opcionales, desactivados por defecto)
Tras una semana de uso, la app pregunta una sola vez si puede enviar informes de errores; puedes cambiar la respuesta
cuando quieras en **Configuración → Enviar informes de errores**. Solo si aceptas, y solo en las versiones oficiales,
la app envía un informe al desarrollador a través de [Sentry](https://sentry.io) cuando ocurre un error. Un informe
contiene:
- el error y en qué parte de la app ocurrió (detalles técnicos como el mensaje de error y la traza);
- la versión de la app e información del dispositivo, como la versión de Android, el modelo, el idioma y la memoria
  libre;
- los eventos técnicos recientes de la app antes del error (por ejemplo, qué pantalla estaba abierta).
- la ubicación aproximada (país, región y ciudad) que Sentry deduce de la dirección IP de la conexión.

Un informe no incluye tus importes, descripciones u otros registros, capturas de pantalla, tu nombre, correo, ID de
usuario ni ID del dispositivo, y el proyecto de Sentry no guarda direcciones IP. Los informes se envían por una conexión
cifrada (HTTPS), se usan solo para encontrar y corregir errores, nunca se venden ni se comparten, y Sentry los borra al
terminar su periodo de conservación.

## Tipos de cambio (opcionales, desactivados por defecto)
Cuando activas **Configuración → Tipos de cambio**, la app descarga tipos de cambio diarios de
[Frankfurter](https://frankfurter.dev) (una vez al día, y cuando tocas **Actualizar ahora**). Una solicitud contiene
solo códigos de moneda y fechas (por ejemplo USD, COP y un rango de fechas), nunca tus importes, descripciones ni
cuentas. Como cualquier solicitud web, revela tu dirección IP a ese servidor. Las tasas se guardan en el dispositivo.

## Registros
La app escribe registros técnicos (errores, versión de la app, versión de Android) en su almacenamiento privado, los
guarda 7 días como máximo y no registra tus importes ni descripciones. Solo salen del dispositivo cuando usas
**Configuración → Compartir registros**.

## Permisos
- **Cámara:** solo cuando tomas la foto de un recibo.
- **Almacenamiento (Android 12 y anteriores):** lo necesita la captura con la cámara en esas versiones.
- **Internet:** se usa solo para los informes de errores y los tipos de cambio, cuando los has activado.
- **Notificaciones (Android 13 y posteriores):** se piden cuando activas los recordatorios, para avisarte de pagos y
  presupuestos. Los recordatorios se crean en el dispositivo; no se envía nada.
- **Biometría / bloqueo de pantalla:** solo cuando activas el bloqueo de la app. Android comprueba tu huella, rostro o
  bloqueo de pantalla; la app solo sabe si se superó y no guarda nada.

## Menores
La app no está dirigida a menores y no recopila datos de nadie a sabiendas.

## Cambios y contacto
Los cambios a esta política se publican en este archivo, con una nueva fecha de vigencia. Preguntas: abre un issue en
https://github.com/SebastianOrtizA/MoneyRecord/issues.
