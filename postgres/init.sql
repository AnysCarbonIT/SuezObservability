CREATE TABLE IF NOT EXISTS messages (
    id uuid PRIMARY KEY,
    message text NOT NULL,
    created_at timestamptz NOT NULL
);
