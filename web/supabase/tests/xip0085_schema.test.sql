begin;

create extension if not exists pgtap with schema extensions;
set local search_path = public, extensions;

select plan(18);

select ok(to_regclass('public.profiles') is not null, 'profiles exists');
select ok(to_regclass('public.gallery_items') is not null, 'gallery_items exists');
select ok(to_regclass('app_private.entitlements') is not null, 'private entitlements exist');
select ok(to_regclass('app_private.operations_ledger_outbox') is not null, 'ledger outbox exists');

select is(
  (select relrowsecurity from pg_catalog.pg_class where oid = 'public.profiles'::regclass),
  true,
  'profiles has RLS enabled'
);
select is(
  (select relrowsecurity from pg_catalog.pg_class where oid = 'public.gallery_items'::regclass),
  true,
  'gallery_items has RLS enabled'
);
select is(has_table_privilege('anon', 'public.profiles', 'SELECT'), false, 'anon cannot read profiles');
select is(has_table_privilege('anon', 'public.gallery_items', 'SELECT'), false, 'anon cannot read gallery items');
select is(has_table_privilege('authenticated', 'public.gallery_items', 'INSERT'), false, 'direct gallery inserts are denied');
select is(has_table_privilege('authenticated', 'public.gallery_items', 'UPDATE'), false, 'direct gallery updates are denied');
select is(has_table_privilege('authenticated', 'public.gallery_items', 'DELETE'), false, 'direct gallery deletes are denied');
select is(has_schema_privilege('anon', 'app_private', 'USAGE'), false, 'anon cannot use private schema');

select ok(to_regprocedure('public.get_my_account_summary()') is not null, 'account summary RPC exists');
select ok(to_regprocedure('public.start_my_trial()') is not null, 'trial RPC exists');
select ok(
  to_regprocedure('public.publish_gallery_item(uuid,text,text,text,text,text,timestamp with time zone,text,text,text)') is not null,
  'publish API RPC exists'
);
select ok(
  to_regprocedure('public.request_gallery_item_unpublish(uuid,text)') is not null,
  'unpublish API RPC exists'
);
select is(
  has_function_privilege('anon', 'public.get_my_account_summary()', 'EXECUTE'),
  false,
  'anon cannot execute account summary'
);
select is(
  (select allow_trial_grants and allow_checkout and allow_publish from app_private.runtime_controls where singleton),
  true,
  'local seed enables staged feature controls'
);

select * from finish();
rollback;
