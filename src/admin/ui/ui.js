/**
 * Клієнт адмінки. Без збірки й залежностей — звичайний ES-модуль, який
 * браузер виконує як є. Тому файл виключено з tsconfig: тут немає типів,
 * а суцільні DOM-звернення під strict дали б сотню порожніх зауважень.
 *
 * Маршрутизація в хеші: #/posts, #/posts/new, #/posts/edit/slug
 */

const nav = document.getElementById('nav');
const main = document.getElementById('main');
const confirmDialog = document.getElementById('confirm');

let collections = [];
/** Чи є незбережені зміни у відкритій формі */
let dirty = false;

// ------------------------------------------------------------------ утиліти

const api = {
  async get(action, params = {}) {
    const url = new URL(`/__admin/api/${action}`, location.origin);
    for (const [key, value] of Object.entries(params)) url.searchParams.set(key, String(value));
    return unwrap(await fetch(url, { headers: { accept: 'application/json' } }));
  },
  async post(action, body) {
    return unwrap(
      await fetch(`/__admin/api/${action}`, {
        method: 'POST',
        headers: { 'content-type': 'application/json' },
        body: JSON.stringify(body),
      }),
    );
  },
};

async function unwrap(response) {
  const data = await response.json().catch(() => ({ error: 'Сервер відповів не по-людськи' }));
  if (!response.ok) throw new Error(data.error || `Помилка ${response.status}`);
  return data;
}

const el = (tag, props = {}, children = []) => {
  const node = Object.assign(document.createElement(tag), props);
  for (const child of [].concat(children)) {
    if (child == null || child === false) continue;
    node.append(typeof child === 'string' ? document.createTextNode(child) : child);
  }
  return node;
};

const MONTHS = ['січ', 'лют', 'бер', 'кві', 'тра', 'чер', 'лип', 'сер', 'вер', 'жов', 'лис', 'гру'];

function formatDate(value) {
  const match = /^(\d{4})-(\d{2})-(\d{2})/.exec(String(value ?? ''));
  if (!match) return String(value ?? '');
  return `${Number(match[3])} ${MONTHS[Number(match[2]) - 1]} ${match[1]}`;
}

const today = () => new Date().toISOString().slice(0, 10);

function findCollection(name) {
  return collections.find((collection) => collection.name === name);
}

// -------------------------------------------------------------- маршрутизація

function go(hash) {
  if (location.hash === hash) render();
  else location.hash = hash;
}

async function guard() {
  if (!dirty) return true;
  return confirm('Є незбережені зміни. Вийти без збереження?');
}

window.addEventListener('hashchange', render);
window.addEventListener('beforeunload', (event) => {
  if (dirty) event.preventDefault();
});

// ---------------------------------------------------------------- бічне меню

async function renderNav() {
  const counts = await Promise.all(
    collections.map((collection) =>
      api
        .get('entries', { collection: collection.name })
        .then((data) => data.entries.length)
        .catch(() => 0),
    ),
  );

  const active = (location.hash.split('/')[1] || collections[0]?.name) ?? '';

  nav.replaceChildren(
    ...collections.map((collection, index) => {
      const button = el('button', { type: 'button' }, [
        collection.label + ' ',
        el('span', { className: 'n', textContent: String(counts[index]) }),
      ]);
      if (collection.name === active) button.setAttribute('aria-current', 'true');
      button.addEventListener('click', async () => {
        if (await guard()) {
          dirty = false;
          go(`#/${collection.name}`);
        }
      });
      return button;
    }),
  );
}

// --------------------------------------------------------------- список

async function renderList(collection) {
  const { entries } = await api.get('entries', { collection: collection.name });

  const newButton = el('button', {
    className: 'btn primary',
    type: 'button',
    textContent: `Додати ${collection.singular}`,
  });
  newButton.addEventListener('click', () => go(`#/${collection.name}/new`));

  const rows = entries.map((entry) => {
    const badges = [];
    if (entry.data.draft) badges.push(el('span', { className: 'badge warn', textContent: 'чернетка' }));
    if (entry.data.status === 'reading') badges.push(el('span', { className: 'badge', textContent: 'читаю' }));

    const meta = entry.data.date
      ? formatDate(entry.data.date)
      : entry.data.year
        ? String(entry.data.year)
        : entry.data.group
          ? String(entry.data.group)
          : '';

    const open = el('button', {
      className: 'btn small',
      type: 'button',
      textContent: 'Редагувати',
    });
    open.addEventListener('click', () => go(`#/${collection.name}/edit/${entry.id}`));

    const row = el('div', { className: 'row' }, [
      el('div', { className: 'grow' }, [
        el('div', { className: 'title', textContent: entry.data.title || entry.id }),
        el('div', { className: 'meta', textContent: [meta, entry.id].filter(Boolean).join(' · ') }),
      ]),
      ...badges,
      open,
    ]);
    return row;
  });

  main.replaceChildren(
    el('div', { className: 'head' }, [el('h2', { textContent: collection.label }), newButton]),
    el('p', {
      className: 'hint',
      textContent: collection.hint ? `${collection.hint} · ${collection.path}` : collection.path,
    }),
    rows.length
      ? el('div', { className: 'rows' }, rows)
      : el('div', { className: 'empty', textContent: 'Поки порожньо. Створи перший запис.' }),
  );
}

// ------------------------------------------------------- редактор markdown

/** Обгортає виділений текст, або вставляє заготовку, якщо нічого не виділено */
function wrapSelection(textarea, before, after = before, placeholder = 'текст') {
  const { selectionStart: start, selectionEnd: end, value } = textarea;
  const selected = value.slice(start, end) || placeholder;
  const inserted = before + selected + after;
  textarea.setRangeText(inserted, start, end, 'end');
  if (!value.slice(start, end)) {
    // Виділяємо заготовку, щоб одразу можна було друкувати поверх
    textarea.setSelectionRange(start + before.length, start + before.length + selected.length);
  }
  textarea.focus();
  textarea.dispatchEvent(new Event('input', { bubbles: true }));
}

/** Ставить префікс на початку кожного виділеного рядка */
function prefixLines(textarea, makePrefix) {
  const { selectionStart, selectionEnd, value } = textarea;
  const from = value.lastIndexOf('\n', selectionStart - 1) + 1;
  const toIndex = value.indexOf('\n', selectionEnd);
  const to = toIndex === -1 ? value.length : toIndex;

  const lines = value.slice(from, to).split('\n');
  const updated = lines.map((line, index) => makePrefix(index) + line).join('\n');

  textarea.setRangeText(updated, from, to, 'end');
  textarea.focus();
  textarea.dispatchEvent(new Event('input', { bubbles: true }));
}

const TOOLS = [
  { label: 'Ж', title: 'Жирний (Ctrl+B)', run: (t) => wrapSelection(t, '**') },
  { label: 'І', title: 'Курсив (Ctrl+I)', run: (t) => wrapSelection(t, '*') },
  { sep: true },
  { label: 'H2', title: 'Підзаголовок', run: (t) => prefixLines(t, () => '## ') },
  { label: 'H3', title: 'Менший підзаголовок', run: (t) => prefixLines(t, () => '### ') },
  { sep: true },
  { label: '🔗', title: 'Посилання (Ctrl+K)', run: (t) => wrapSelection(t, '[', '](https://)', 'підпис') },
  { label: '❝', title: 'Цитата', run: (t) => prefixLines(t, () => '> ') },
  { label: '•', title: 'Список', run: (t) => prefixLines(t, () => '- ') },
  { label: '1.', title: 'Нумерований список', run: (t) => prefixLines(t, (i) => `${i + 1}. `) },
  { sep: true },
  { label: '<>', title: 'Код у рядку', run: (t) => wrapSelection(t, '`', '`', 'код') },
  { label: '{ }', title: 'Блок коду', run: (t) => wrapSelection(t, '```\n', '\n```', 'код') },
  { label: '—', title: 'Роздільник', run: (t) => wrapSelection(t, '\n---\n', '', '') },
];

function buildEditor(value, onInput) {
  const textarea = el('textarea', { value, spellcheck: true });
  textarea.addEventListener('input', onInput);

  const preview = el('div', { className: 'preview', hidden: true });

  const toggle = el('button', {
    type: 'button',
    textContent: 'Передперегляд',
    title: 'Показати, як це виглядатиме на сайті',
  });
  toggle.setAttribute('aria-pressed', 'false');

  toggle.addEventListener('click', async () => {
    const showing = toggle.getAttribute('aria-pressed') === 'true';
    if (showing) {
      toggle.setAttribute('aria-pressed', 'false');
      preview.hidden = true;
      textarea.hidden = false;
      return;
    }
    toggle.setAttribute('aria-pressed', 'true');
    textarea.hidden = true;
    preview.hidden = false;
    preview.textContent = 'Рендеримо…';
    try {
      const { html } = await api.post('preview', { markdown: textarea.value });
      preview.innerHTML = html;
    } catch (error) {
      preview.textContent = `Не вдалося: ${error.message}`;
    }
  });

  const buttons = TOOLS.map((tool) => {
    if (tool.sep) return el('span', { className: 'sep' });
    const button = el('button', { type: 'button', textContent: tool.label, title: tool.title });
    button.addEventListener('click', (event) => {
      event.preventDefault();
      tool.run(textarea);
    });
    return button;
  });

  textarea.addEventListener('keydown', (event) => {
    if (!event.ctrlKey && !event.metaKey) return;
    const key = event.key.toLowerCase();
    if (key === 'b') {
      event.preventDefault();
      wrapSelection(textarea, '**');
    } else if (key === 'i') {
      event.preventDefault();
      wrapSelection(textarea, '*');
    } else if (key === 'k') {
      event.preventDefault();
      wrapSelection(textarea, '[', '](https://)', 'підпис');
    }
  });

  const toolbar = el('div', { className: 'toolbar' }, [
    ...buttons,
    el('span', { className: 'spacer' }),
    toggle,
  ]);

  return {
    node: el('div', { className: 'editor' }, [toolbar, textarea, preview]),
    get value() {
      return textarea.value;
    },
  };
}

// ------------------------------------------------------------------- форма

function buildField(field, value, onInput) {
  const id = `f-${field.name}`;
  const label = el('label', { htmlFor: id }, [
    field.label,
    field.required ? el('span', { className: 'req', textContent: ' *' }) : null,
  ]);
  const help = field.help ? el('div', { className: 'help', textContent: field.help }) : null;

  let control;
  let read;

  switch (field.widget) {
    case 'markdown': {
      const editor = buildEditor(value ?? '', onInput);
      control = editor.node;
      read = () => editor.value;
      break;
    }

    case 'textarea': {
      control = el('textarea', { className: 'control', id, value: value ?? '', rows: 3 });
      control.addEventListener('input', onInput);
      read = () => control.value;
      break;
    }

    case 'boolean': {
      control = el('input', { className: 'control', type: 'checkbox', id, checked: Boolean(value) });
      control.addEventListener('change', onInput);
      read = () => control.checked;
      break;
    }

    case 'select': {
      control = el(
        'select',
        { className: 'control', id },
        field.options.map((option) =>
          el('option', { value: option.value, textContent: option.label, selected: option.value === value }),
        ),
      );
      control.addEventListener('change', onInput);
      read = () => control.value;
      break;
    }

    case 'number': {
      control = el('input', {
        className: 'control',
        type: 'number',
        id,
        value: value ?? '',
        min: field.min ?? '',
        max: field.max ?? '',
        step: field.step ?? 'any',
      });
      control.addEventListener('input', onInput);
      read = () => control.value;
      break;
    }

    case 'date': {
      control = el('input', { className: 'control', type: 'date', id, value: value ?? '' });
      control.addEventListener('input', onInput);
      read = () => control.value;
      break;
    }

    case 'list': {
      control = el('input', {
        className: 'control',
        type: 'text',
        id,
        value: Array.isArray(value) ? value.join(', ') : (value ?? ''),
      });
      control.addEventListener('input', onInput);
      read = () => control.value;
      break;
    }

    default: {
      control = el('input', {
        className: 'control',
        type: field.widget === 'url' ? 'url' : 'text',
        id,
        value: value ?? '',
      });
      control.addEventListener('input', onInput);
      read = () => control.value;
    }
  }

  const inline = field.widget === 'boolean';
  const wrapper = el(
    'div',
    { className: inline ? 'field inline' : 'field' },
    inline ? [control, label, help] : [label, control, help],
  );

  return { node: wrapper, read };
}

async function renderForm(collection, id) {
  const isNew = !id;
  let entry = { id: '', data: {}, body: '' };

  if (!isNew) {
    const result = await api.get('entry', { collection: collection.name, id });
    entry = result.entry;
  } else {
    // Розумні значення за замовчуванням
    for (const field of collection.fields) {
      if (field.widget === 'date' && field.required) entry.data[field.name] = today();
      if (field.widget === 'select') entry.data[field.name] = field.options[0].value;
    }
  }

  const status = el('div', { className: 'status' });
  const markDirty = () => {
    dirty = true;
    status.textContent = '';
    status.className = 'status';
  };

  const fields = collection.fields.map((field) => {
    const value = field.widget === 'markdown' ? entry.body : entry.data[field.name];
    return { field, ...buildField(field, value, markDirty) };
  });

  // Адреса запису — тільки для markdown, бо там ім’я файлу = URL
  let slugInput = null;
  if (collection.kind === 'markdown') {
    slugInput = el('input', {
      className: 'control',
      type: 'text',
      id: 'f-slug',
      value: entry.id,
      placeholder: 'зробиться із заголовка',
    });
    slugInput.addEventListener('input', markDirty);
  }

  const collect = () => {
    const payload = {};
    for (const { field, read } of fields) payload[field.name] = read();
    return payload;
  };

  // ---- кнопки
  const saveButton = el('button', { className: 'btn primary', type: 'submit', textContent: 'Зберегти' });

  const backButton = el('button', { className: 'btn', type: 'button', textContent: 'Назад' });
  backButton.addEventListener('click', async () => {
    if (await guard()) {
      dirty = false;
      go(`#/${collection.name}`);
    }
  });

  const deleteButton = el('button', { className: 'btn danger', type: 'button', textContent: 'Видалити' });
  deleteButton.addEventListener('click', () => askDelete(collection, entry));

  const form = el('form', {}, [
    el('div', { className: 'head' }, [
      el('h2', { textContent: isNew ? `Додати ${collection.singular}` : entry.data.title || entry.id }),
    ]),
    el('p', { className: 'hint' }, [
      collection.kind === 'markdown'
        ? el('code', { className: 'path', textContent: `${collection.path}/${entry.id || '…'}.md` })
        : el('code', { className: 'path', textContent: collection.path }),
    ]),
    ...fields.map((item) => item.node),
    slugInput
      ? el('div', { className: 'field' }, [
          el('label', { htmlFor: 'f-slug', textContent: 'Адреса' }),
          slugInput,
          el('div', {
            className: 'help',
            textContent:
              'Латиниця, цифри, дефіс. Якщо змінити в наявного запису — зміниться посилання на нього.',
          }),
        ])
      : null,
    el('div', { className: 'actions' }, [
      saveButton,
      backButton,
      el('span', { className: 'spacer' }),
      status,
      !isNew && collection.canDelete ? deleteButton : null,
    ]),
  ]);

  form.addEventListener('submit', async (event) => {
    event.preventDefault();
    saveButton.disabled = true;
    status.className = 'status';
    status.textContent = 'Зберігаємо…';

    try {
      const result = await api.post('save', {
        collection: collection.name,
        id: isNew ? undefined : entry.id,
        newId: slugInput ? slugInput.value.trim() : undefined,
        payload: collect(),
      });

      dirty = false;
      status.className = 'status ok';
      status.textContent = 'Збережено';
      await renderNav();

      // Після створення (або перейменування) переходимо на актуальну адресу
      if (result.entry.id !== entry.id) {
        go(`#/${collection.name}/edit/${result.entry.id}`);
        return;
      }
      entry = result.entry;
    } catch (error) {
      status.className = 'status err';
      status.textContent = error.message;
    } finally {
      saveButton.disabled = false;
    }
  });

  main.replaceChildren(form);
}

// ------------------------------------------------------------------ видалення

function askDelete(collection, entry) {
  const text = document.getElementById('confirm-text');
  text.textContent = `«${entry.data.title || entry.id}» буде стерто з диска. Скасувати це неможливо.`;

  const yes = document.getElementById('confirm-yes');
  const no = document.getElementById('confirm-no');

  const cleanup = () => {
    yes.removeEventListener('click', onYes);
    no.removeEventListener('click', onNo);
    confirmDialog.close();
  };
  const onNo = () => cleanup();
  const onYes = async () => {
    cleanup();
    try {
      await api.post('delete', { collection: collection.name, id: entry.id });
      dirty = false;
      await renderNav();
      go(`#/${collection.name}`);
    } catch (error) {
      alert(`Не вдалося видалити: ${error.message}`);
    }
  };

  yes.addEventListener('click', onYes);
  no.addEventListener('click', onNo);
  confirmDialog.showModal();
}

// ------------------------------------------------------------------- рендер

async function render() {
  const [, name, mode, id] = location.hash.replace(/^#\//, '/').split('/');
  const collection = findCollection(name) ?? collections[0];

  if (!collection) {
    main.replaceChildren(el('p', { className: 'hint', textContent: 'Колекцій не знайдено.' }));
    return;
  }

  if (!findCollection(name)) {
    location.replace(`#/${collection.name}`);
    return;
  }

  try {
    if (mode === 'new') await renderForm(collection, null);
    else if (mode === 'edit' && id) await renderForm(collection, decodeURIComponent(id));
    else await renderList(collection);
  } catch (error) {
    main.replaceChildren(
      el('div', { className: 'head' }, [el('h2', { textContent: 'Помилка' })]),
      el('p', { className: 'status err', textContent: error.message }),
      el('p', { className: 'hint', textContent: 'Перевір консоль дев-сервера — там буде більше деталей.' }),
    );
  }

  await renderNav();
}

async function start() {
  try {
    const data = await api.get('schema');
    collections = data.collections;
  } catch (error) {
    main.replaceChildren(
      el('p', { className: 'status err', textContent: `Не вдалося завантажити схему: ${error.message}` }),
    );
    return;
  }

  if (!location.hash) location.replace(`#/${collections[0].name}`);
  await render();
}

start();
