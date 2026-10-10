import { useState, type FormEvent } from 'react';
import {
  hasErrors,
  validateFilters,
  type FieldErrors,
  type FilterField,
  type SearchFilters,
} from '../domain/searchFilters';

interface Props {
  values: SearchFilters;
  cities: string[];
  /** Problems the server reported for individual parameters. */
  serverErrors: FieldErrors;
  busy: boolean;
  onChange: (field: FilterField, value: string) => void;
  onSubmit: () => void;
  onReset: () => void;
}

interface FieldDef {
  name: FilterField;
  label: string;
  hint: string;
  inputMode?: 'decimal' | 'numeric' | 'text';
  list?: string;
}

const FIELDS: FieldDef[] = [
  { name: 'minPrice', label: 'Min price ($)', hint: 'e.g. 250000', inputMode: 'decimal' },
  { name: 'maxPrice', label: 'Max price ($)', hint: 'e.g. 600000', inputMode: 'decimal' },
  { name: 'minBedrooms', label: 'Min bedrooms', hint: 'e.g. 3', inputMode: 'numeric' },
  { name: 'city', label: 'City', hint: 'e.g. Springfield', list: 'city-options' },
  { name: 'keyword', label: 'Keyword', hint: 'searches descriptions, e.g. fenced yard' },
  { name: 'targetBudget', label: 'Target budget ($)', hint: 'ranks closer prices higher', inputMode: 'decimal' },
];

export const FORM_FIELD_NAMES: readonly string[] = FIELDS.map((field) => field.name);

export function SearchForm({ values, cities, serverErrors, busy, onChange, onSubmit, onReset }: Props) {
  const [localErrors, setLocalErrors] = useState<FieldErrors>({});

  const handleSubmit = (event: FormEvent) => {
    event.preventDefault();
    const errors = validateFilters(values);
    setLocalErrors(errors);
    if (!hasErrors(errors)) onSubmit();
  };

  const handleChange = (field: FilterField, value: string) => {
    setLocalErrors(({ [field]: _cleared, ...rest }) => rest);
    onChange(field, value);
  };

  const handleReset = () => {
    setLocalErrors({});
    onReset();
  };

  return (
    <form className="search-form" onSubmit={handleSubmit} noValidate aria-label="Listing filters">
      <div className="fields">
        {FIELDS.map((field) => {
          const messages = localErrors[field.name] ?? serverErrors[field.name] ?? [];
          const errorId = `${field.name}-error`;
          return (
            <div className="field" key={field.name}>
              <label htmlFor={field.name}>{field.label}</label>
              <input
                id={field.name}
                name={field.name}
                type="text"
                inputMode={field.inputMode}
                list={field.list}
                placeholder={field.hint}
                autoComplete="off"
                value={values[field.name]}
                aria-invalid={messages.length > 0}
                aria-describedby={messages.length > 0 ? errorId : undefined}
                onChange={(event) => handleChange(field.name, event.target.value)}
              />
              {messages.length > 0 && (
                <p className="field-error" id={errorId}>
                  {messages.join(' ')}
                </p>
              )}
            </div>
          );
        })}
      </div>

      <datalist id="city-options">
        {cities.map((city) => (
          <option key={city} value={city} />
        ))}
      </datalist>

      <div className="actions">
        <button type="submit" disabled={busy}>
          Search
        </button>
        <button type="button" className="secondary" onClick={handleReset} disabled={busy}>
          Reset
        </button>
      </div>
    </form>
  );
}
