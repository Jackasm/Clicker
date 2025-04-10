# Удалённое управление презентациями  

**Управляйте презентациями со смартфона через локальную сеть**  

---

## 🔥 Основные возможности  
- **Автоматическое подключение** по QR-коду (определение IP-адреса)  
- **Простой интерфейс** с крупными сенсорными кнопками  
- **Поддержка программ**:  
  - Microsoft PowerPoint  
  - Google Slides  
  - Adobe Acrobat Reader (PDF-презентации)  
  - Любое ПО, реагирующее на стрелки вниз/вверх  
- **Работа без интернета** - использует локальную Wi-Fi сеть  

---

## ⚠️ Важные требования  
**Программа требует запуска от имени администратора**  
Это необходимо для:  
- Создания HTTP-сервера на порту 5000  
- Автоматического добавления правил брандмауэра  

---

## 🚀 Инструкция по использованию  

1. **Запустите программу** правой кнопкой → "Запуск от имени администратора"  
2. **Отсканируйте QR-код** смартфоном  
3. **Откройте ссылку** в мобильном браузере  

---

## ⚙️ Установка  

### Для пользователей:  
1. Скачайте последнюю версию:  
   [Releases](https://github.com/Jackasm/Clicker/releases)  
2. Запустите `Clicker.exe` от имени администратора  

### Для разработчиков:  
```bash
git clone https://github.com/Jackasm/Clicker.git
```
**Системные требования:**  
- Windows 7/8/10/11  
- .NET Framework 4.7.2+  
- Права администратора при запуске  

---

## 🔧 Техническая информация  
- **Сервер**: Встроенный HTTP-сервер на C#  
- **Безопасность**: Защита токеном при подключении  
- **Зависимости**:  
  - QRCoder для генерации QR-кодов      

---

## 📜 Лицензионная информация  
Программа распространяется под лицензией MIT.  
Полный текст: [LICENSE](LICENSE)  

---

## Благодарности  
Используемые технологии:  
- [QRCoder](https://github.com/codebude/QRCoder) - генерация QR-кодов  

---

**Совет:** Для удобства можно создать ярлык с обязательным запуском от администратора (в свойствах ярлыка → Дополнительно → Запуск от имени администратора).
