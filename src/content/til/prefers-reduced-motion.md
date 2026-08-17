---
title: CSS вміє питати про тему системи
date: 2026-07-28
tags: [css]
source: https://developer.mozilla.org/en-US/docs/Web/CSS/@media/prefers-color-scheme
---

Темна тема не потребує ані кнопки, ані JavaScript — достатньо одного медіазапиту:

```css
@media (prefers-color-scheme: dark) {
  :root { --bg: #16161a; --text: #e6e4e0; }
}
```

Кнопка потрібна лише тим, хто хоче відрізнятися від системної теми. На цьому
сайті вона є, але й без неї все працює.
