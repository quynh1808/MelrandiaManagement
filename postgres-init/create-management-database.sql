\set ON_ERROR_STOP on

-- Run as the PostgreSQL superuser. Password assignment deliberately uses the
-- interactive psql \password command documented in Docs/05, never this file.
SELECT 'CREATE ROLE melrandia_app LOGIN'
WHERE NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'melrandia_app') \gexec

SELECT 'CREATE DATABASE melrandia_management OWNER melrandia_app'
WHERE NOT EXISTS (SELECT 1 FROM pg_database WHERE datname = 'melrandia_management') \gexec

\connect melrandia_management
ALTER SCHEMA public OWNER TO melrandia_app;
GRANT ALL ON SCHEMA public TO melrandia_app;
